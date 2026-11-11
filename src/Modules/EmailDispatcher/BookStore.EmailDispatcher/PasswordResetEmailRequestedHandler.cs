namespace BookStore.EmailDispatcher;

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
        var encodedName = HtmlEncoder.Default.Encode(value.FirstName);
        var encodedLink = HtmlEncoder.Default.Encode(link);
        var message = new EmailMessage(settings.Value.FromAddress, value.Email, "Reset your BookStore password",
            $"Hello {value.FirstName},\n\nReset your password by visiting:\n{link}\n\nIf you did not request this, you can ignore this email.",
            $"<p>Hello {encodedName},</p><p>Reset your password by following <a href=\"{encodedLink}\">this link</a>.</p><p>If you did not request this, you can ignore this email.</p>");

        return sender.SendAsync(message, cancellationToken);
    }
}
