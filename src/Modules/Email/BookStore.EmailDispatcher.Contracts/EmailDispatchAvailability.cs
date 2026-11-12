namespace BookStore.EmailDispatcher.Contracts;

public sealed record EmailDispatchAvailability(bool IsAvailable, DateTimeOffset? RetryAfter);
