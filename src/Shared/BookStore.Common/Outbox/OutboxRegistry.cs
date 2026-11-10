namespace BookStore.Common.Outbox;

public sealed class OutboxRegistry<TStore> where TStore : class, IOutboxStore
{
    private readonly Dictionary<string, IOutboxRegistration> _registrations = new(StringComparer.Ordinal);

    public OutboxRegistry<TStore> Register<TEvent>(string eventType) where TEvent : IEvent
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        _registrations.Add(eventType, new Registration<TEvent>());

        return this;
    }

    internal IOutboxRegistration Get(string eventType) =>
        _registrations.TryGetValue(eventType, out var registration)
            ? registration
            : throw new InvalidOperationException("Unknown outbox event type.");

    private sealed class Registration<TEvent> : IOutboxRegistration where TEvent : IEvent
    {
        public async Task<OutboxDispatchResult> DispatchAsync(IServiceProvider services, string payload,
            DateTimeOffset now, CancellationToken cancellationToken)
        {
            var value = JsonSerializer.Deserialize<TEvent>(payload)
                ?? throw new JsonException("Outbox event deserialized to null.");
            var expiry = services.GetService<IOutboxExpiry<TEvent>>();
            if (expiry?.IsExpired(value, now) is true)
            {
                return new(OutboxDispatchStatus.Expired);
            }

            var preflight = services.GetService<IOutboxPreflight<TEvent>>();
            if (preflight is not null)
            {
                var availability = await preflight.CheckAsync(value, cancellationToken);
                if (!availability.Available)
                {
                    return new(OutboxDispatchStatus.Deferred, availability.RetryAfter);
                }
            }

            await services.GetRequiredService<IEventPublisher<TEvent>>().PublishAsync(value, cancellationToken);

            return new(OutboxDispatchStatus.Completed);
        }
    }
}

internal enum OutboxDispatchStatus { Completed, Deferred, Expired }
internal sealed record OutboxDispatchResult(OutboxDispatchStatus Status, DateTimeOffset? RetryAfter = null);
internal interface IOutboxRegistration
{
    Task<OutboxDispatchResult> DispatchAsync(IServiceProvider services, string payload, DateTimeOffset now,
        CancellationToken cancellationToken);
}
