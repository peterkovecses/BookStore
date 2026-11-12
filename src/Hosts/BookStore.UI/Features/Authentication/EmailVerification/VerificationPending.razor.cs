namespace BookStore.UI.Features.Authentication.EmailVerification;

internal static partial class VerificationPendingLogging
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Verification resend countdown storage is unavailable.")]
    internal static partial void LogStorageUnavailable(this ILogger<VerificationPending> logger, Exception exception);
}
