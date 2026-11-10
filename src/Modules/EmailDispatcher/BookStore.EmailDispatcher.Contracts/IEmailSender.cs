namespace BookStore.EmailDispatcher.Contracts;

public sealed record EmailMessage(string From, string To, string Subject, string TextBody, string? HtmlBody = null);

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}

public interface IEmailDispatchAvailability
{
    EmailDispatchAvailability GetAvailability();
}

public sealed record EmailDispatchAvailability(bool IsAvailable, DateTimeOffset? RetryAfter);

public sealed class EmailCircuitOpenException(DateTimeOffset? retryAfter)
    : Exception("The email dispatcher circuit is open.")
{
    public DateTimeOffset? RetryAfter { get; } = retryAfter;
}
