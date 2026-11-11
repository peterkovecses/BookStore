namespace BookStore.EmailDispatcher;

internal static class VerificationEmailTemplate
{
    internal static EmailMessage Create(string from, string to, string firstName, string link)
    {
        var encodedName = HtmlEncoder.Default.Encode(firstName);
        var encodedLink = HtmlEncoder.Default.Encode(link);

        return new EmailMessage(
            from,
            to,
            "Verify your BookStore email address",
            $"Hello {firstName},\n\nVerify your email address by visiting:\n{link}\n\nIf you did not request this, you can ignore this email.",
            $"<p>Hello {encodedName},</p><p>Verify your email address by following <a href=\"{encodedLink}\">this link</a>.</p><p>If you did not request this, you can ignore this email.</p>");
    }
}
