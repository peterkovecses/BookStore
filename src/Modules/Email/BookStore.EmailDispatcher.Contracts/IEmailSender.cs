namespace BookStore.EmailDispatcher.Contracts;

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
