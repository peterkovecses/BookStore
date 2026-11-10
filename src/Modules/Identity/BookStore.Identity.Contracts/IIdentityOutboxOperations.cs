namespace BookStore.Identity.Contracts;

/// <summary>Outbox administration for trusted, authorized callers. This interface performs no authentication.</summary>
public interface IIdentityOutboxOperations
{
    Task<bool> ReplayAsync(Guid eventId, string actor, CancellationToken cancellationToken);
}
