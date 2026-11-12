namespace BookStore.EmailDispatcher.IntegrationTests;

[Collection("Email Integration")]
public sealed class MailpitTests
{
    [Fact]
    public async Task PublishAsync_WhenIdentityEventsArePublished_ShouldSendMimeAlternativesToMailpit()
    {
        // Arrange
        const int smtpPort = 1025;
        const int httpPort = 8025;
        const string email = "reader@example.com";
        const string from = "sender@example.com";
        const string name = "<Alice & Bob>";
        const string verifyLink = "https://example.com/verify?token=abc&name=\"<script>\"";
        const string resetLink = "https://example.com/reset?token=xyz&name=\"<script>\"";
        await using var container = new ContainerBuilder("axllent/mailpit:latest")
            .WithPortBinding(smtpPort, true)
            .WithPortBinding(httpPort, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(smtpPort))
            .Build();
        await container.StartAsync(TestContext.Current.CancellationToken);
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = Environments.Development });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["EmailDispatcher:Smtp:Host"] = container.Hostname,
            ["EmailDispatcher:Smtp:FromAddress"] = "sender@example.com",
            ["EmailDispatcher:Smtp:Port"] = container.GetMappedPublicPort(smtpPort).ToString(),
            ["EmailDispatcher:Smtp:Security"] = "None"
        });
        var protection = new EphemeralDataProtectionProvider();
        builder.Services.AddSingleton<IDataProtectionProvider>(protection);
        builder.AddEmailDispatcherModuleServices();
        builder.Services.AddRequests(Array.Empty<System.Reflection.Assembly>());
        using var host = builder.Build();
        await host.StartAsync(TestContext.Current.CancellationToken);
        using var scope = host.Services.CreateScope();
        var verification = scope.ServiceProvider.GetRequiredService<IEventPublisher<VerificationEmailRequested>>();
        var passwordReset = scope.ServiceProvider.GetRequiredService<IEventPublisher<PasswordResetEmailRequested>>();
        var verificationEvent = new VerificationEmailRequested("user-id", email, name,
            protection.CreateProtector("BookStore.Identity.VerificationLink.v1")
                .CreateProtector("user-id").CreateProtector(email).Protect(verifyLink), DateTimeOffset.MaxValue);
        var resetEvent = new PasswordResetEmailRequested("user-id", email, name,
            protection.CreateProtector("BookStore.Identity.PasswordResetLink.v2")
                .CreateProtector("user-id").Protect(resetLink), DateTimeOffset.MaxValue);

        // Act
        await verification.PublishAsync(verificationEvent, TestContext.Current.CancellationToken);
        await passwordReset.PublishAsync(resetEvent, TestContext.Current.CancellationToken);
        using var http = new HttpClient { BaseAddress = new Uri($"http://{container.Hostname}:{container.GetMappedPublicPort(httpPort)}") };
        using var listing = JsonDocument.Parse(await http.GetStringAsync("/api/v1/messages", TestContext.Current.CancellationToken));
        var messages = new List<MimeMessage>();
        foreach (var item in listing.RootElement.GetProperty("messages").EnumerateArray())
        {
            var id = item.GetProperty("ID").GetString();
            await using var raw = await http.GetStreamAsync($"/api/v1/message/{id}/raw", TestContext.Current.CancellationToken);
            messages.Add(await MimeMessage.LoadAsync(raw, TestContext.Current.CancellationToken));
        }

        // Assert
        messages.Should().HaveCount(2);
        AssertMessage(messages.Single(message => message.Subject == "Verify your BookStore email address"), from, email, name, verifyLink, verificationEvent.ProtectedLink);
        AssertMessage(messages.Single(message => message.Subject == "Reset your BookStore password"), from, email, name, resetLink, resetEvent.ProtectedLink);
        await host.StopAsync(TestContext.Current.CancellationToken);
    }

    private static void AssertMessage(MimeMessage message, string from, string email, string name, string link, string protectedLink)
    {
        message.From.Mailboxes.Single().Address.Should().Be(from);
        message.To.Mailboxes.Single().Address.Should().Be(email);
        message.Body.Should().BeOfType<MultipartAlternative>();
        message.TextBody.Should().Contain(name).And.Contain(link);
        message.HtmlBody.Should().Contain(HtmlEncoder.Default.Encode(name)).And.Contain(HtmlEncoder.Default.Encode(link))
            .And.NotContain(protectedLink);
    }
}
