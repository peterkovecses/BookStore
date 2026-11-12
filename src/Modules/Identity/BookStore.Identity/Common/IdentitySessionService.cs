namespace BookStore.Identity.Common;

internal sealed class IdentitySessionService(
    IIdentityDbContext context,
    UserManager<ApplicationUser> users,
    IOptions<AuthenticationOptions> options,
    TimeProvider timeProvider)
{
    private readonly AuthenticationOptions _options = options.Value;

    internal async Task<AuthenticationResult> LoginAsync(string email, string password, CancellationToken cancellationToken)
    {
        var normalizedEmail = users.NormalizeEmail(email);
        var lockKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedEmail)));

        return await context.ExecuteInTransactionAsync(async (transaction, token) =>
        {
            await IdentityTransactionLock.AcquireAsync(transaction, "login:" + lockKey, token);
            var userId = await context.Users.AsNoTracking().Where(item => item.NormalizedEmail == normalizedEmail)
                .Select(item => item.Id).SingleOrDefaultAsync(token);
            if (userId is null)
            {
                await transaction.RollbackAsync(token);

                return AuthenticationResult.Invalid;
            }

            await IdentityTransactionLock.AcquireAsync(transaction, IdentityTransactionLock.User(userId), token);
            var user = await users.FindByEmailAsync(email);
            if (user is null || await users.IsLockedOutAsync(user))
            {
                await transaction.RollbackAsync(token);

                return AuthenticationResult.Invalid;
            }

            if (!await users.CheckPasswordAsync(user, password))
            {
                var failed = await users.AccessFailedAsync(user);
                if (!failed.Succeeded)
                {
                    throw new InvalidOperationException("Failed to update account lockout.");
                }

                await transaction.CommitAsync(token);

                return AuthenticationResult.Invalid;
            }

            var reset = await users.ResetAccessFailedCountAsync(user);
            if (!reset.Succeeded)
            {
                throw new InvalidOperationException("Failed to reset account lockout.");
            }

            if (!user.EmailConfirmed)
            {
                await transaction.CommitAsync(token);

                return AuthenticationResult.Unconfirmed;
            }

            var result = await IssueAsync(user, Guid.NewGuid(), cancellationToken);
            await transaction.CommitAsync(token);

            return result;
        }, cancellationToken);
    }

    internal async Task<AuthenticationResult> RefreshAsync(string rawToken, CancellationToken cancellationToken)
    {
        var hash = Hash(rawToken);

        return await context.ExecuteInTransactionAsync(async (transaction, token) =>
        {
            var session = await context.RefreshTokens.AsNoTracking().Where(item => item.Hash == hash)
                .Select(item => new { item.FamilyId, item.UserId }).SingleOrDefaultAsync(token);
            if (session is null)
            {
                await transaction.RollbackAsync(token);

                return AuthenticationResult.Invalid;
            }

            await IdentityTransactionLock.AcquireAsync(transaction, IdentityTransactionLock.User(session.UserId), token);
            await IdentityTransactionLock.AcquireAsync(transaction, "refresh:" + session.FamilyId.ToString("N"), token);
            var refreshToken = await context.RefreshTokens.SingleAsync(item => item.Hash == hash, token);
            if (refreshToken.Rotated)
            {
                await context.RefreshTokens.Where(item => item.FamilyId == session.FamilyId && !item.Revoked)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Revoked, true), token);
                await transaction.CommitAsync(token);

                return AuthenticationResult.Invalid;
            }

            if (refreshToken.Revoked || refreshToken.ExpiresAt <= timeProvider.GetUtcNow())
            {
                await transaction.RollbackAsync(token);

                return AuthenticationResult.Invalid;
            }

            var user = await users.FindByIdAsync(refreshToken.UserId);
            if (user is null || !user.EmailConfirmed)
            {
                await transaction.RollbackAsync(token);

                return AuthenticationResult.Invalid;
            }

            refreshToken.Rotated = true;
            var result = await IssueAsync(user, session.FamilyId, cancellationToken);
            await transaction.CommitAsync(token);

            return result;
        }, cancellationToken);
    }

    internal async Task LogoutAsync(string rawToken, CancellationToken cancellationToken)
    {
        var hash = Hash(rawToken);
        await context.ExecuteInTransactionAsync(async (transaction, token) =>
        {
            var family = await context.RefreshTokens.AsNoTracking().Where(item => item.Hash == hash)
                .Select(item => (Guid?)item.FamilyId).SingleOrDefaultAsync(token);
            if (family is null)
            {
                await transaction.RollbackAsync(token);

                return false;
            }

            await IdentityTransactionLock.AcquireAsync(transaction, "refresh:" + family.Value.ToString("N"), token);
            await context.RefreshTokens.Where(token => token.FamilyId == family.Value && !token.Revoked)
                .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.Revoked, true), token);
            await transaction.CommitAsync(token);

            return true;
        }, cancellationToken);
    }

    private async Task<AuthenticationResult> IssueAsync(ApplicationUser user, Guid family, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var rawToken = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        context.RefreshTokens.Add(new RefreshToken
        {
            Hash = Hash(rawToken),
            FamilyId = family,
            UserId = user.Id,
            ExpiresAt = now.Add(_options.RefreshTokenLifetime)
        });
        await context.SaveChangesAsync(cancellationToken);
        var expiresAt = now.Add(_options.AccessTokenLifetime);
        var jwt = new JwtSecurityToken(
            _options.Issuer,
            _options.Audience,
            [new Claim(JwtRegisteredClaimNames.Sub, user.Id), new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())],
            now.UtcDateTime,
            expiresAt.UtcDateTime,
            new SigningCredentials(new SymmetricSecurityKey(Convert.FromBase64String(_options.SigningKey)), SecurityAlgorithms.HmacSha256));

        return new AuthenticationResult(new AccessTokenResponse(new JwtSecurityTokenHandler().WriteToken(jwt), expiresAt), rawToken, false);
    }

    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

internal sealed record AuthenticationResult(AccessTokenResponse? AccessToken, string? RefreshToken, bool EmailUnconfirmed)
{
    internal static AuthenticationResult Invalid { get; } = new(null, null, false);
    internal static AuthenticationResult Unconfirmed { get; } = new(null, null, true);
}
