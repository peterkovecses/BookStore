namespace BookStore.EmailDispatcher.Infrastructure.Smtp;

internal sealed class SmtpAuthenticationFailedException(Exception innerException)
    : Exception("SMTP authentication failed.", innerException);
