namespace BookStore.Common.Outbox;

public sealed record OutboxClaim(Guid Id, string EventType, string Payload, string CorrelationId,
    Guid Token, int AttemptCount)
{
    public DateTimeOffset CreatedAt { get; init; }
}

public sealed record OutboxAvailability(bool Available, DateTimeOffset? RetryAfter = null);

public sealed record OutboxDiagnostics(long Pending, long Processing, long Failed, long Expired, DateTimeOffset? OldestPending);

public sealed class OutboxDispatchContext
{
    public Guid EventId { get; private set; }
    public string CorrelationId { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }

    internal void Set(OutboxClaim claim)
    {
        EventId = claim.Id;
        CorrelationId = claim.CorrelationId;
        CreatedAt = claim.CreatedAt;
    }
}

public interface IOutboxStore
{
    Task<IReadOnlyList<OutboxClaim>> ClaimAsync(int count, OutboxOptions options, CancellationToken cancellationToken);
    Task<bool> RenewAsync(OutboxClaim claim, OutboxOptions options, CancellationToken cancellationToken);
    Task<bool> CompleteAsync(OutboxClaim claim, CancellationToken cancellationToken);
    Task<bool> FailAsync(OutboxClaim claim, string error, TimeSpan delay, int maxAttempts, CancellationToken cancellationToken);
    Task<bool> DeferAsync(OutboxClaim claim, DateTimeOffset nextAttempt, CancellationToken cancellationToken);
    Task<bool> ExpireAsync(OutboxClaim claim, CancellationToken cancellationToken);
    Task<int> CleanupAsync(DateTimeOffset cutoff, int batchSize, CancellationToken cancellationToken);
    Task<OutboxDiagnostics> GetDiagnosticsAsync(CancellationToken cancellationToken);
}

public interface IOutboxPreflight<in TEvent> where TEvent : IEvent
{
    ValueTask<OutboxAvailability> CheckAsync(TEvent value, CancellationToken cancellationToken);
}

public interface IOutboxExpiry<in TEvent> where TEvent : IEvent
{
    bool IsExpired(TEvent value, DateTimeOffset now);

    bool IsExpired(TEvent value, DateTimeOffset now, DateTimeOffset createdAt) => IsExpired(value, now);
}

public sealed class OutboxOptions
{
    public int BatchSize { get; set; } = 20;
    public int Concurrency { get; set; } = 4;
    public int MaxAttempts { get; set; } = 8;
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);
    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan RenewalInterval { get; set; } = TimeSpan.FromSeconds(60);
    public TimeSpan HandlerTimeout { get; set; } = TimeSpan.FromMinutes(3);
    public TimeSpan ShutdownTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan RetryInitialDelay { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan RetryMaximumDelay { get; set; } = TimeSpan.FromMinutes(30);
    public TimeSpan PreflightFallbackDelay { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromHours(24);
    public TimeSpan Retention { get; set; } = TimeSpan.FromDays(7);
    public int CleanupBatchSize { get; set; } = 100;
    public TimeSpan DiagnosticsInterval { get; set; } = TimeSpan.FromMinutes(1);

    public bool IsValid() =>
        BatchSize is > 0 and <= 1000 && Concurrency is > 0 and <= 100 && MaxAttempts is > 0 and <= 1000 &&
        IsTimerDuration(PollInterval) && LeaseDuration.TotalMilliseconds >= 1 && LeaseDuration.TotalMilliseconds <= int.MaxValue && IsTimerDuration(RenewalInterval) &&
        RenewalInterval < LeaseDuration && HandlerTimeout > TimeSpan.Zero && HandlerTimeout < LeaseDuration &&
        IsTimerDuration(ShutdownTimeout) && RetryInitialDelay.TotalMilliseconds >= 1 &&
        RetryMaximumDelay >= RetryInitialDelay && RetryMaximumDelay.TotalMilliseconds <= int.MaxValue && IsTimerDuration(PreflightFallbackDelay) &&
        IsTimerDuration(CleanupInterval) && Retention > TimeSpan.Zero && CleanupBatchSize is > 0 and <= 10000 &&
        IsTimerDuration(DiagnosticsInterval);

    private static bool IsTimerDuration(TimeSpan value) => value > TimeSpan.Zero && value.TotalMilliseconds <= uint.MaxValue - 1;
}
