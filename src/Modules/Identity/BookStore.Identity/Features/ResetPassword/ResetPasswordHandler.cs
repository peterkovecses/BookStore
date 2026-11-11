namespace BookStore.Identity.Features.ResetPassword;

internal sealed class ResetPasswordHandler(IdentityDbContext context, UserManager<ApplicationUser> users)
    : IRequestHandler<ResetPasswordCommand, Result>
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public async Task<Result> HandleAsync(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        string token;
        try
        {
            token = StrictUtf8.GetString(WebEncoders.Base64UrlDecode(request.Token));
        }
        catch (FormatException)
        {
            return Invalid();
        }
        catch (DecoderFallbackException)
        {
            return Invalid();
        }

        return await context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            context.ChangeTracker.Clear();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            await IdentityTransactionLock.AcquireAsync(context, IdentityTransactionLock.User(request.UserId), cancellationToken);
            var user = await users.FindByIdAsync(request.UserId);
            if (user is null)
            {
                return Invalid();
            }

            var result = await users.ResetPasswordAsync(user, token, request.NewPassword);
            if (!result.Succeeded)
            {
                return Invalid();
            }

            if (!user.EmailConfirmed)
            {
                user.EmailConfirmed = true;
                var update = await users.UpdateAsync(user);
                if (!update.Succeeded)
                {
                    throw new InvalidOperationException("Failed to confirm the account during password recovery.");
                }
            }

            await context.RefreshTokens.Where(item => item.UserId == user.Id && !item.Revoked)
                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Revoked, true), cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result.Success();
        });
    }

    private static Result Invalid() =>
        Result.Failure(Error.Validation("PasswordReset", "The password reset request could not be completed."));
}
