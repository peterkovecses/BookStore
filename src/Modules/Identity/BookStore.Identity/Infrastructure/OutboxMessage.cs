namespace BookStore.Identity.Infrastructure;

internal enum OutboxStatus
{
    Pending,
    Processing,
    Completed,
    Failed,
    Expired
}

internal sealed class OutboxMessage
{
    public Guid Id { get; init; }
    public required string EventType { get; init; }
    public required string Payload { get; init; }
    public required string CorrelationId { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public OutboxStatus Status { get; set; }
    public int AttemptCount { get; set; }
    public long CumulativeAttemptCount { get; set; }
    public DateTimeOffset? LastAttemptAt { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
    public string? LastError { get; set; }
    public Guid? ClaimToken { get; set; }
    public DateTimeOffset? LeaseExpiresAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public int ProcessingCycle { get; set; }
    public int ReplayCount { get; set; }
    public DateTimeOffset? LastReplayedAt { get; set; }
    public string? LastReplayedBy { get; set; }
}
