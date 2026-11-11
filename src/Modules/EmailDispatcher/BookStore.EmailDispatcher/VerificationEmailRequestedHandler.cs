using System.Text.Encodings.Web;
using BookStore.Identity.Contracts;
using Kovecses.Requests;
using Microsoft.AspNetCore.DataProtection;

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
        var encodedName = HtmlEncoder.Default.Encode(@event.FirstName);
        var encodedLink = HtmlEncoder.Default.Encode(link);
        var message = new EmailMessage(
            settings.Value.FromAddress,
            @event.Email,
            "Verify your BookStore email address",
            $"Hello {@event.FirstName},\n\nVerify your email address by visiting:\n{link}\n\nIf you did not request this, you can ignore this email.",
            $"<p>Hello {encodedName},</p><p>Verify your email address by following <a href=\"{encodedLink}\">this link</a>.</p><p>If you did not request this, you can ignore this email.</p>");

        return sender.SendAsync(message, cancellationToken);
    }
}
