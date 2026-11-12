namespace BookStore.EmailDispatcher.Features.PasswordReset;

internal static class PasswordResetEmailTemplate
{
    internal static EmailMessage Create(string from, string to, string firstName, string link)
    {
        var encodedName = HtmlEncoder.Default.Encode(firstName);
        var encodedLink = HtmlEncoder.Default.Encode(link);

        return new EmailMessage(
            from,
            to,
            "Reset your BookStore password",
            $"Hello {firstName},\n\nReset your password by visiting:\n{link}\n\nIf you did not request this, you can ignore this email.",
            $"<p>Hello {encodedName},</p><p>Reset your password by following <a href=\"{encodedLink}\">this link</a>.</p><p>If you did not request this, you can ignore this email.</p>");
    }
}
