namespace BookStore.Identity.Infrastructure;

internal sealed class PasswordRecoveryRequestReceipt
{
    public Guid RequestEventId { get; init; }
    public Guid? EmailEventId { get; init; }
    public DateTimeOffset ProcessedAt { get; init; }
}
