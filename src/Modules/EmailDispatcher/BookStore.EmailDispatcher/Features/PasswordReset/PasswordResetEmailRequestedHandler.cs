namespace BookStore.EmailDispatcher.Features.PasswordReset;

internal sealed class PasswordResetEmailRequestedHandler(
    IEmailSender sender,
    IDataProtectionProvider protection,
    IOptions<SmtpSettings> settings) : IEventHandler<PasswordResetEmailRequested>
{
    public Task HandleAsync(PasswordResetEmailRequested value, CancellationToken cancellationToken)
    {
        if (!MailboxAddress.TryParse(settings.Value.FromAddress, out _))
        {
            throw new InvalidOperationException("EmailDispatcher:Smtp:FromAddress must be configured with a valid email address.");
        }

        var link = protection.CreateProtector("BookStore.Identity.PasswordResetLink.v1")
            .CreateProtector(value.UserId).CreateProtector(value.Email).Unprotect(value.ProtectedLink);
        var message = PasswordResetEmailTemplate.Create(settings.Value.FromAddress, value.Email, value.FirstName, link);

        return sender.SendAsync(message, cancellationToken);
    }
}
