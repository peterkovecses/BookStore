namespace BookStore.EmailDispatcher.Features.Verification;

internal sealed class VerificationEmailRequestedHandler(
    IEmailSender sender,
    IDataProtectionProvider dataProtectionProvider,
    IOptions<SmtpSettings> settings,
    EmailBodyRenderer renderer) : IEventHandler<VerificationEmailRequested>
{
    public async Task HandleAsync(VerificationEmailRequested @event, CancellationToken cancellationToken)
    {
        if (!MailboxAddress.TryParse(settings.Value.FromAddress, out _))
        {
            throw new InvalidOperationException("EmailDispatcher:Smtp:FromAddress must be configured with a valid email address.");
        }

        var link = dataProtectionProvider.CreateProtector("BookStore.Identity.VerificationLink.v1")
            .CreateProtector(@event.UserId)
            .CreateProtector(@event.Email)
            .Unprotect(@event.ProtectedLink);
        var body = await renderer.RenderVerificationAsync(@event.FirstName, link, cancellationToken);
        var message = new EmailMessage(settings.Value.FromAddress, @event.Email, "Verify your BookStore email address", body.TextBody, body.HtmlBody);

        await sender.SendAsync(message, cancellationToken);
    }
}
