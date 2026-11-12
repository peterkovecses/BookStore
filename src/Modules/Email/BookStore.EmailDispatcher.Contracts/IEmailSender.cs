namespace BookStore.EmailDispatcher.Contracts;

/// <summary>Sends email messages through the configured dispatcher.</summary>
public interface IEmailSender
{
    /// <summary>Sends a message to the configured email transport.</summary>
    /// <param name="message">The message to send.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>A task that completes when the transport accepts the message; this does not guarantee delivery.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="message"/> is null.</exception>
    /// <exception cref="EmailCircuitOpenException">The email dispatcher circuit is open.</exception>
    /// <exception cref="OperationCanceledException">The operation was canceled.</exception>
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
