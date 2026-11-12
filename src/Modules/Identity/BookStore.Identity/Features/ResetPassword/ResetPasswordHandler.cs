namespace BookStore.Identity.Features.ResetPassword;

internal sealed class ResetPasswordHandler(IIdentityDbContext context, UserManager<ApplicationUser> users)
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

        var resetToken = token;

        return await context.ExecuteInTransactionAsync(async (transaction, cancellationToken) =>
        {
            await IdentityTransactionLock.AcquireAsync(transaction, IdentityTransactionLock.User(request.UserId), cancellationToken);
            var user = await users.FindByIdAsync(request.UserId);
            if (user is null)
            {
                await transaction.RollbackAsync(cancellationToken);

                return Invalid();
            }

            var result = await users.ResetPasswordAsync(user, resetToken, request.NewPassword);
            if (!result.Succeeded)
            {
                await transaction.RollbackAsync(cancellationToken);

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
        }, cancellationToken);
    }

    private static Result Invalid() =>
        Result.Failure(Error.Validation("PasswordReset", "The password reset request could not be completed."));
}
