namespace BookStore.Api;

internal sealed class SmtpOutboxPreflight<TEvent>(IEmailDispatchAvailability availability) : IOutboxPreflight<TEvent>
    where TEvent : IEvent
{
    public ValueTask<OutboxAvailability> CheckAsync(TEvent value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var current = availability.GetAvailability();

        return ValueTask.FromResult(new OutboxAvailability(current.IsAvailable, current.RetryAfter));
    }
}
