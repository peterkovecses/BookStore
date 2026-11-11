using Microsoft.AspNetCore.DataProtection;

namespace BookStore.EmailDispatcher.IntegrationTests;

public sealed class MailpitTests
{
    [Fact]
    public async Task SendAsync_WhenMailpitConfigured_ShouldAcceptMessage()
    {
        // Arrange
        const int smtpPort = 1025;
        const int httpPort = 8025;
        const string subject = "BookStore dispatcher integration test";
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
            ["EmailDispatcher:Smtp:Port"] = container.GetMappedPublicPort(smtpPort).ToString(),
            ["EmailDispatcher:Smtp:Security"] = "None"
        });
        builder.Services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
        builder.AddEmailDispatcherModuleServices();
        using var host = builder.Build();
        await host.StartAsync(TestContext.Current.CancellationToken);
        var sut = host.Services.GetRequiredService<IEmailSender>();

        // Act
        await sut.SendAsync(new("sender@example.com", "reader@example.com", subject, "Test body"), TestContext.Current.CancellationToken);
        using var http = new HttpClient { BaseAddress = new Uri($"http://{container.Hostname}:{container.GetMappedPublicPort(httpPort)}") };
        var actual = await http.GetStringAsync("/api/v1/messages", TestContext.Current.CancellationToken);

        // Assert
        actual.Should().Contain(subject);
        await host.StopAsync(TestContext.Current.CancellationToken);
    }
}
