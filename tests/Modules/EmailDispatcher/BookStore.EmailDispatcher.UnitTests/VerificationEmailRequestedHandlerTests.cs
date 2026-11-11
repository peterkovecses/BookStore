using System.Security.Cryptography;
using BookStore.Identity.Contracts;
using Microsoft.AspNetCore.DataProtection;

namespace BookStore.EmailDispatcher.UnitTests;

public sealed class VerificationEmailRequestedHandlerTests
{
    private const string Purpose = "BookStore.Identity.VerificationLink.v1";
    private const string FromAddress = "sender@example.com";
    private readonly IDataProtectionProvider _provider = new EphemeralDataProtectionProvider();
    private readonly IEmailSender _sender = Substitute.For<IEmailSender>();

    [Fact]
    public async Task HandleAsync_WhenLinkAndNameContainHtml_ShouldSendEncodedHtmlAndPlainText()
    {
        // Arrange
        const string name = "<Alice & Bob>";
        const string link = "https://example.com/verify?token=abc&name=\"test\"";
        const string email = "reader@example.com";
        var value = new VerificationEmailRequested("user-id", email, name, Protect(link, "user-id", email), DateTimeOffset.MaxValue);
        var sut = new VerificationEmailRequestedHandler(_sender, _provider, Options.Create(new SmtpSettings { FromAddress = FromAddress }));
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        await sut.HandleAsync(value, cancellationToken);

        // Assert
        await _sender.Received(1).SendAsync(Arg.Is<EmailMessage>(message =>
            message.From == FromAddress &&
            message.To == email &&
            message.Subject.Contains("Verify") &&
            message.TextBody.Contains(name) &&
            message.TextBody.Contains(link) &&
            message.HtmlBody != null &&
            message.HtmlBody.Contains("&lt;Alice &amp; Bob&gt;") &&
            message.HtmlBody.Contains("https://example.com/verify?token=abc&amp;name=&quot;test&quot;") &&
            !message.HtmlBody.Contains(value.ProtectedLink)), cancellationToken);
    }

    [Fact]
    public async Task HandleAsync_WhenProtectedLinkWasTampered_ShouldNotSend()
    {
        // Arrange
        var value = new VerificationEmailRequested("user-id", "reader@example.com", "Alice", "invalid", DateTimeOffset.MaxValue);
        var sut = new VerificationEmailRequestedHandler(_sender, _provider, Options.Create(new SmtpSettings { FromAddress = FromAddress }));

        // Act
        Func<Task> act = () => sut.HandleAsync(value, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<CryptographicException>();
        await _sender.DidNotReceive().SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenSenderFails_ShouldPropagateFailure()
    {
        // Arrange
        var value = new VerificationEmailRequested("user-id", "reader@example.com", "Alice", Protect("https://example.com", "user-id", "reader@example.com"), DateTimeOffset.MaxValue);
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _sender.SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>()).Returns(completion.Task);
        var sut = new VerificationEmailRequestedHandler(_sender, _provider, Options.Create(new SmtpSettings { FromAddress = FromAddress }));

        // Act
        var actual = sut.HandleAsync(value, TestContext.Current.CancellationToken);
        completion.SetException(new InvalidOperationException());

        // Assert
        Func<Task> act = () => actual;
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task HandleAsync_WhenFromAddressIsMissing_ShouldNotSend()
    {
        // Arrange
        var value = new VerificationEmailRequested("user-id", "reader@example.com", "Alice", Protect("https://example.com", "user-id", "reader@example.com"), DateTimeOffset.MaxValue);
        var sut = new VerificationEmailRequestedHandler(_sender, _provider, Options.Create(new SmtpSettings()));

        // Act
        Func<Task> act = () => sut.HandleAsync(value, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        await _sender.DidNotReceive().SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenRecipientWasChanged_ShouldNotSend()
    {
        // Arrange
        var value = new VerificationEmailRequested("user-id", "attacker@example.com", "Alice",
            Protect("https://example.com", "user-id", "reader@example.com"), DateTimeOffset.MaxValue);
        var sut = new VerificationEmailRequestedHandler(_sender, _provider, Options.Create(new SmtpSettings { FromAddress = FromAddress }));

        // Act
        Func<Task> act = () => sut.HandleAsync(value, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<CryptographicException>();
        await _sender.DidNotReceive().SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }

    private string Protect(string link, string userId, string email) =>
        _provider.CreateProtector(Purpose).CreateProtector(userId).CreateProtector(email).Protect(link);
}
