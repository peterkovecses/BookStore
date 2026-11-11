using System.Security.Cryptography;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.IdentityModel.Tokens;

namespace BookStore.Identity.Features.Authentication;

internal sealed class AuthenticationService(
    IdentityDbContext context,
    UserManager<ApplicationUser> users,
    IOptions<AuthenticationOptions> options,
    TimeProvider timeProvider)
{
    private readonly AuthenticationOptions _options = options.Value;

    internal async Task<AuthenticationResult> LoginAsync(string email, string password, CancellationToken cancellationToken)
    {
        var normalizedEmail = users.NormalizeEmail(email);
        var lockKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedEmail)));

        return await context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            context.ChangeTracker.Clear();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            await AcquireLockAsync("login:" + lockKey, cancellationToken);
            var user = await users.FindByEmailAsync(email);
            if (user is null || await users.IsLockedOutAsync(user))
            {
                return AuthenticationResult.Invalid;
            }

            if (!await users.CheckPasswordAsync(user, password))
            {
                var failed = await users.AccessFailedAsync(user);
                if (!failed.Succeeded)
                {
                    throw new InvalidOperationException("Failed to update account lockout.");
                }

                await transaction.CommitAsync(cancellationToken);

                return AuthenticationResult.Invalid;
            }

            var reset = await users.ResetAccessFailedCountAsync(user);
            if (!reset.Succeeded)
            {
                throw new InvalidOperationException("Failed to reset account lockout.");
            }

            if (!user.EmailConfirmed)
            {
                await transaction.CommitAsync(cancellationToken);

                return AuthenticationResult.Unconfirmed;
            }

            var result = await IssueAsync(user, Guid.NewGuid(), cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return result;
        });
    }

    internal async Task<AuthenticationResult> RefreshAsync(string rawToken, CancellationToken cancellationToken)
    {
        var hash = Hash(rawToken);

        return await context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            context.ChangeTracker.Clear();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            var family = await context.RefreshTokens.AsNoTracking().Where(token => token.Hash == hash)
                .Select(token => (Guid?)token.FamilyId).SingleOrDefaultAsync(cancellationToken);
            if (family is null)
            {
                return AuthenticationResult.Invalid;
            }

            await AcquireLockAsync("refresh:" + family.Value.ToString("N"), cancellationToken);
            var token = await context.RefreshTokens.SingleAsync(item => item.Hash == hash, cancellationToken);
            if (token.Rotated)
            {
                await context.RefreshTokens.Where(item => item.FamilyId == family.Value && !item.Revoked)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Revoked, true), cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return AuthenticationResult.Invalid;
            }

            if (token.Revoked || token.ExpiresAt <= timeProvider.GetUtcNow())
            {
                return AuthenticationResult.Invalid;
            }

            var user = await users.FindByIdAsync(token.UserId);
            if (user is null || !user.EmailConfirmed)
            {
                return AuthenticationResult.Invalid;
            }

            token.Rotated = true;
            var result = await IssueAsync(user, family.Value, cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return result;
        });
    }

    internal async Task LogoutAsync(string rawToken, CancellationToken cancellationToken)
    {
        var hash = Hash(rawToken);
        await context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            context.ChangeTracker.Clear();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            var family = await context.RefreshTokens.AsNoTracking().Where(token => token.Hash == hash)
                .Select(token => (Guid?)token.FamilyId).SingleOrDefaultAsync(cancellationToken);
            if (family is null)
            {
                return;
            }

            await AcquireLockAsync("refresh:" + family.Value.ToString("N"), cancellationToken);
            await context.RefreshTokens.Where(token => token.FamilyId == family.Value && !token.Revoked)
                .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.Revoked, true), cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });
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

    private async Task AcquireLockAsync(string resource, CancellationToken cancellationToken)
    {
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.Transaction = context.Database.CurrentTransaction!.GetDbTransaction();
        command.CommandText = "DECLARE @result int; EXEC @result = sp_getapplock @Resource = @resource, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000; IF @result < 0 THROW 51000, 'Could not acquire identity lock.', 1;";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@resource";
        parameter.Value = resource;
        command.Parameters.Add(parameter);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

internal sealed record AuthenticationResult(AccessTokenResponse? AccessToken, string? RefreshToken, bool EmailUnconfirmed)
{
    internal static AuthenticationResult Invalid { get; } = new(null, null, false);
    internal static AuthenticationResult Unconfirmed { get; } = new(null, null, true);
}
