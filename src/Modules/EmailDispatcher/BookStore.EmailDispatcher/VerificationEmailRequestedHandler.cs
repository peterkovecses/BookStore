namespace BookStore.EmailDispatcher;

internal sealed class VerificationEmailRequestedHandler(
    IEmailSender sender,
    IDataProtectionProvider dataProtectionProvider,
    IOptions<SmtpSettings> settings) : IEventHandler<VerificationEmailRequested>
{
    public Task HandleAsync(VerificationEmailRequested @event, CancellationToken cancellationToken)
    {
        if (!MailboxAddress.TryParse(settings.Value.FromAddress, out _))
        {
            throw new InvalidOperationException("EmailDispatcher:Smtp:FromAddress must be configured with a valid email address.");
        }

        var link = dataProtectionProvider.CreateProtector("BookStore.Identity.VerificationLink.v1")
            .CreateProtector(@event.UserId)
            .CreateProtector(@event.Email)
            .Unprotect(@event.ProtectedLink);
        var message = VerificationEmailTemplate.Create(settings.Value.FromAddress, @event.Email, @event.FirstName, link);

        return sender.SendAsync(message, cancellationToken);
    }
}
