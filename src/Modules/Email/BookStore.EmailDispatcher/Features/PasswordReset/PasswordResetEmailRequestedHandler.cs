namespace BookStore.EmailDispatcher.Features.PasswordReset;

internal sealed class PasswordResetEmailRequestedHandler(
    IEmailSender sender,
    IDataProtectionProvider protection,
    IOptions<SmtpSettings> settings,
    EmailBodyRenderer renderer) : IEventHandler<PasswordResetEmailRequested>
{
    public async Task HandleAsync(PasswordResetEmailRequested value, CancellationToken cancellationToken)
    {
        if (!MailboxAddress.TryParse(settings.Value.FromAddress, out _))
        {
            throw new InvalidOperationException("EmailDispatcher:Smtp:FromAddress must be configured with a valid email address.");
        }

        string link;
        try
        {
            link = protection.CreateProtector("BookStore.Identity.PasswordResetLink.v2")
                .CreateProtector(value.UserId).Unprotect(value.ProtectedLink);
        }
        catch (CryptographicException)
        {
            link = protection.CreateProtector("BookStore.Identity.PasswordResetLink.v1")
                .CreateProtector(value.UserId).CreateProtector(value.Email).Unprotect(value.ProtectedLink);
        }

        var body = await renderer.RenderPasswordResetAsync(value.FirstName, link, cancellationToken);
        var message = new EmailMessage(settings.Value.FromAddress, value.Email, "Reset your BookStore password", body.TextBody, body.HtmlBody);

        await sender.SendAsync(message, cancellationToken);
    }
}
