namespace BookStore.Identity.Features.Verification;

internal sealed class ConfirmEmailHandler(UserManager<ApplicationUser> users)
    : IRequestHandler<ConfirmEmailCommand, Result>
{
    private const string ConfirmationErrorCode = "EmailConfirmation";
    private const string ConfirmationErrorMessage = "The verification request could not be completed.";
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public async Task<Result> HandleAsync(ConfirmEmailCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await users.FindByIdAsync(request.UserId);
        if (user is null)
        {
            return InvalidRequest();
        }

        if (await users.IsEmailConfirmedAsync(user))
        {
            return Result.Success();
        }

        string token;
        try
        {
            token = StrictUtf8.GetString(WebEncoders.Base64UrlDecode(request.EncodedToken));
        }
        catch (FormatException)
        {
            return InvalidRequest();
        }
        catch (DecoderFallbackException)
        {
            return InvalidRequest();
        }

        cancellationToken.ThrowIfCancellationRequested();
        var result = await users.ConfirmEmailAsync(user, token);
        if (!result.Succeeded)
        {
            return InvalidRequest();
        }

        return Result.Success();
    }

    private static Result InvalidRequest() =>
        Result.Failure(Error.Validation(ConfirmationErrorCode, ConfirmationErrorMessage));
}
