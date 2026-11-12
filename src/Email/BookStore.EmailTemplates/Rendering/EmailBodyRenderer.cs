namespace BookStore.EmailTemplates.Rendering;

public sealed class EmailBodyRenderer(IServiceProvider serviceProvider, ILoggerFactory loggerFactory)
{
    public Task<EmailBody> RenderVerificationAsync(string firstName, string link, CancellationToken cancellationToken) =>
        RenderAsync(firstName, link, "Verify your email address",
            "Welcome to BookStore! Please verify your email address to finish setting up your account.",
            "Verify email address", cancellationToken);

    public Task<EmailBody> RenderPasswordResetAsync(string firstName, string link, CancellationToken cancellationToken) =>
        RenderAsync(firstName, link, "Reset your password",
            "We received a request to reset your BookStore password. Use the link below to choose a new password.",
            "Reset password", cancellationToken);

    private async Task<EmailBody> RenderAsync(
        string firstName,
        string link,
        string heading,
        string message,
        string action,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        await using var renderer = new HtmlRenderer(serviceProvider, loggerFactory);
        var parameters = ParameterView.FromDictionary(new Dictionary<string, object?>
        {
            [nameof(IdentityEmailTemplate.FirstName)] = firstName,
            [nameof(IdentityEmailTemplate.Link)] = link,
            [nameof(IdentityEmailTemplate.Heading)] = heading,
            [nameof(IdentityEmailTemplate.Message)] = message,
            [nameof(IdentityEmailTemplate.Action)] = action
        });

        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var output = await renderer.RenderComponentAsync<IdentityEmailTemplate>(parameters);
            cancellationToken.ThrowIfCancellationRequested();

            return output.ToHtmlString();
        });

        cancellationToken.ThrowIfCancellationRequested();
        var text = $"BookStore\n\n{heading}\n\nHello {firstName},\n\n{message}\n\n{action}:\n{link}\n\nIf you did not request this, you can ignore this email.";

        return new EmailBody(text, html);
    }
}
