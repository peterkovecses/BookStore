namespace BookStore.UI.Features.Authentication.PasswordRecovery;

internal static partial class ForgotPasswordLogging
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Password recovery countdown storage is unavailable.")]
    internal static partial void LogStorageUnavailable(this ILogger<ForgotPassword> logger, Exception exception);
}
