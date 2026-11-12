namespace BookStore.Identity.Contracts;

/// <summary>Outbox administration for trusted, authorized callers. This interface performs no authentication.</summary>
public interface IIdentityOutboxOperations
{
    /// <summary>Returns a failed or completed outbox event to the pending state for another processing cycle.</summary>
    /// <param name="eventId">The identifier of the event to replay.</param>
    /// <param name="actor">The trusted caller's identifier, recorded for audit purposes; at most 256 characters.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns><see langword="true"/> if the event was returned to pending; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="actor"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="actor"/> is empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="actor"/> exceeds 256 characters.</exception>
    /// <exception cref="OperationCanceledException">The operation was canceled.</exception>
    Task<bool> ReplayAsync(Guid eventId, string actor, CancellationToken cancellationToken);
}
