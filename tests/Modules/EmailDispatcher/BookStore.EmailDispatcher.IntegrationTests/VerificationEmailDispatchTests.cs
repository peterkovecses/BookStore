using BookStore.Identity.Contracts;
using Kovecses.Requests;
using Microsoft.AspNetCore.DataProtection;
using NSubstitute;

namespace BookStore.EmailDispatcher.IntegrationTests;

public sealed class VerificationEmailDispatchTests
{
    [Fact]
    public async Task PublishAsync_WhenSubscriberIsRegistered_ShouldSendVerificationEmail()
    {
        // Arrange
        const string link = "https://example.com/verify?token=test";
        const string email = "reader@example.com";
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = Environments.Development });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["EmailDispatcher:Smtp:Host"] = "localhost",
            ["EmailDispatcher:Smtp:FromAddress"] = "sender@example.com"
        });
        builder.AddEmailDispatcherModuleServices();
        builder.Services.AddRequests(Array.Empty<System.Reflection.Assembly>());
        var provider = new EphemeralDataProtectionProvider();
        builder.Services.AddSingleton<IDataProtectionProvider>(provider);
        var sender = Substitute.For<IEmailSender>();
        builder.Services.AddSingleton(sender);
        using var host = builder.Build();
        using var scope = host.Services.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<IEventPublisher<VerificationEmailRequested>>();
        var value = new VerificationEmailRequested("user-id", email, "Alice",
            provider.CreateProtector("BookStore.Identity.VerificationLink.v1")
                .CreateProtector("user-id").CreateProtector(email).Protect(link), DateTimeOffset.MaxValue);

        // Act
        await sut.PublishAsync(value, TestContext.Current.CancellationToken);

        // Assert
        await sender.Received(1).SendAsync(Arg.Is<EmailMessage>(message =>
            message.To == email && message.TextBody.Contains(link) && message.HtmlBody != null && message.HtmlBody.Contains(link)),
            TestContext.Current.CancellationToken);
    }
}
