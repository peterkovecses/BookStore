namespace BookStore.EmailDispatcher.IntegrationTests;

public sealed class PasswordResetEmailDispatchTests
{
    [Fact]
    public async Task PublishAsync_WhenHandlerIsRegistered_ShouldSendBothAlternatives()
    {
        // Arrange
        const string link = "https://example.com/reset?token=secret";
        const string email = "reader@example.com";
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = Environments.Development });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["EmailDispatcher:Smtp:Host"] = "localhost",
            ["EmailDispatcher:Smtp:FromAddress"] = "sender@example.com"
        });
        builder.AddEmailDispatcherModuleServices();
        builder.Services.AddRequests(Array.Empty<System.Reflection.Assembly>());
        var protection = new EphemeralDataProtectionProvider();
        builder.Services.AddSingleton<IDataProtectionProvider>(protection);
        var sender = Substitute.For<IEmailSender>();
        builder.Services.AddSingleton(sender);
        using var host = builder.Build();
        using var scope = host.Services.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<IEventPublisher<PasswordResetEmailRequested>>();
        var value = new PasswordResetEmailRequested("user", email, "<Alice>",
            protection.CreateProtector("BookStore.Identity.PasswordResetLink.v1")
                .CreateProtector("user").CreateProtector(email).Protect(link), DateTimeOffset.MaxValue);

        // Act
        await sut.PublishAsync(value, TestContext.Current.CancellationToken);

        // Assert
        await sender.Received(1).SendAsync(Arg.Is<EmailMessage>(message =>
            message.To == email && message.TextBody.Contains(link) &&
            message.HtmlBody != null && message.HtmlBody.Contains(link) &&
            message.HtmlBody.Contains("&lt;Alice&gt;")), TestContext.Current.CancellationToken);
    }
}
