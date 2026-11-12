namespace BookStore.EmailDispatcher.Contracts;

public sealed class EmailCircuitOpenException(DateTimeOffset? retryAfter)
    : Exception("The email dispatcher circuit is open.")
{
    public DateTimeOffset? RetryAfter { get; } = retryAfter;
}
