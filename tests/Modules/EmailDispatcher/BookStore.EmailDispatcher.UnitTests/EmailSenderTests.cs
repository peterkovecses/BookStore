using BookStore.EmailDispatcher.Contracts;
using System.Net.Sockets;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using MimeKit;

namespace BookStore.EmailDispatcher.UnitTests;

public sealed class EmailSenderTests
{
    private static readonly EmailMessage Message = new("sender@example.com", "recipient@example.com", "Subject", "Body");

    private static EmailSender CreateSender(SmtpSettings settings, FakeTimeProvider clock, Func<ISmtpClient> factory)
    {
        var options = Options.Create(settings);

        return new(options, new SmtpCircuitBreaker(options, clock), clock, NullLogger<EmailSender>.Instance, factory);
    }

    [Fact]
    public async Task SendAsync_WhenConnected_ShouldReuseClient()
    {
        // Arrange
        var client = Substitute.For<ISmtpClient>();
        client.IsConnected.Returns(true);
        var clock = new FakeTimeProvider();
        await using var sut = CreateSender(new(), clock, () => client);

        // Act
        await sut.SendAsync(Message, TestContext.Current.CancellationToken);
        await sut.SendAsync(Message, TestContext.Current.CancellationToken);

        // Assert
        await client.Received(2).SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>(), Arg.Any<ITransferProgress>());
        await client.DidNotReceive().ConnectAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<SecureSocketOptions>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_WhenServerDisconnected_ShouldReconnect()
    {
        // Arrange
        const string host = "localhost";
        const int port = 2525;
        var client = Substitute.For<ISmtpClient>();
        client.IsConnected.Returns(false, true, false);
        var clock = new FakeTimeProvider();
        await using var sut = CreateSender(new() { Host = host, Port = port }, clock, () => client);

        // Act
        await sut.SendAsync(Message, TestContext.Current.CancellationToken);
        await sut.SendAsync(Message, TestContext.Current.CancellationToken);
        await sut.SendAsync(Message, TestContext.Current.CancellationToken);

        // Assert
        await client.Received(2).ConnectAsync(host, port, SecureSocketOptions.StartTls, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_WhenTlsAndAuthenticationConfigured_ShouldUseBoth()
    {
        // Arrange
        const string host = "smtp.example.com";
        const string username = "user";
        const string password = "password";
        const int port = 465;
        var client = Substitute.For<ISmtpClient>();
        var clock = new FakeTimeProvider();
        await using var sut = CreateSender(new() { Host = host, Port = port, Security = SecureSocketOptions.SslOnConnect, Username = username, Password = password }, clock, () => client);

        // Act
        await sut.SendAsync(Message, TestContext.Current.CancellationToken);

        // Assert
        await client.Received(1).ConnectAsync(host, port, SecureSocketOptions.SslOnConnect, Arg.Any<CancellationToken>());
        await client.Received(1).AuthenticateAsync(username, password, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_WhenAuthenticationFails_ShouldNotRetry()
    {
        // Arrange
        var client = Substitute.For<ISmtpClient>();
        client.AuthenticateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new MailKit.Security.AuthenticationException());
        var clock = new FakeTimeProvider();
        await using var sut = CreateSender(new() { Username = "user", Password = "password" }, clock, () => client);

        // Act
        var act = () => sut.SendAsync(Message, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<SmtpAuthenticationFailedException>();
        await client.Received(1).AuthenticateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_WhenPermanentResponse_ShouldPropagateWithoutRetry()
    {
        // Arrange
        var client = Substitute.For<ISmtpClient>();
        client.IsConnected.Returns(true);
        client.SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>(), Arg.Any<ITransferProgress>())
            .Returns<Task<string>>(_ => throw new SmtpCommandException(SmtpErrorCode.RecipientNotAccepted, SmtpStatusCode.MailboxUnavailable, "rejected"));
        var clock = new FakeTimeProvider();
        await using var sut = CreateSender(new(), clock, () => client);

        // Act
        var act = () => sut.SendAsync(Message, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<SmtpCommandException>();
        await client.Received(1).SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>(), Arg.Any<ITransferProgress>());
    }

    [Fact]
    public async Task SendAsync_WhenHostDoesNotExist_ShouldNotRetry()
    {
        // Arrange
        var client = Substitute.For<ISmtpClient>();
        client.ConnectAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<SecureSocketOptions>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new SocketException((int)SocketError.HostNotFound));
        var clock = new FakeTimeProvider();
        await using var sut = CreateSender(new(), clock, () => client);

        // Act
        var act = () => sut.SendAsync(Message, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<SocketException>();
        await client.Received(1).ConnectAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<SecureSocketOptions>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_WhenTransientFailureThenSuccess_ShouldRetryWithNewClient()
    {
        // Arrange
        var failed = Substitute.For<ISmtpClient>();
        var recovered = Substitute.For<ISmtpClient>();
        failed.IsConnected.Returns(true);
        recovered.IsConnected.Returns(true);
        failed.SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>(), Arg.Any<ITransferProgress>())
            .Returns<Task<string>>(_ => throw new IOException());
        var clock = new FakeTimeProvider();
        var clients = new Queue<ISmtpClient>([failed, recovered]);
        await using var sut = CreateSender(new(), clock, clients.Dequeue);

        // Act
        var actual = sut.SendAsync(Message, TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromSeconds(3));
        await actual;

        // Assert
        failed.Received(1).Dispose();
        await recovered.Received(1).SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>(), Arg.Any<ITransferProgress>());
    }

    [Fact]
    public async Task SendAsync_WhenTemporarySmtpResponse_ShouldRetry()
    {
        // Arrange
        var failed = Substitute.For<ISmtpClient>();
        var recovered = Substitute.For<ISmtpClient>();
        failed.IsConnected.Returns(true);
        recovered.IsConnected.Returns(true);
        failed.SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>(), Arg.Any<ITransferProgress>())
            .Returns<Task<string>>(_ => throw new SmtpCommandException(SmtpErrorCode.RecipientNotAccepted, SmtpStatusCode.MailboxBusy, "temporary failure"));
        var clock = new FakeTimeProvider();
        var clients = new Queue<ISmtpClient>([failed, recovered]);
        await using var sut = CreateSender(new(), clock, clients.Dequeue);

        // Act
        var actual = sut.SendAsync(Message, TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromSeconds(3));
        await actual;

        // Assert
        await recovered.Received(1).SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>(), Arg.Any<ITransferProgress>());
    }

    [Fact]
    public async Task SendAsync_WhenCancelled_ShouldNotRetry()
    {
        // Arrange
        var client = Substitute.For<ISmtpClient>();
        client.IsConnected.Returns(true);
        var clock = new FakeTimeProvider();
        await using var sut = CreateSender(new(), clock, () => client);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        // Act
        var act = () => sut.SendAsync(Message, cancellation.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        await client.DidNotReceive().SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>(), Arg.Any<ITransferProgress>());
    }

    [Fact]
    public async Task SendAsync_WhenCancelledDuringRetryDelay_ShouldNotRetry()
    {
        // Arrange
        var client = Substitute.For<ISmtpClient>();
        client.IsConnected.Returns(true);
        client.SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>(), Arg.Any<ITransferProgress>())
            .Returns<Task<string>>(_ => throw new IOException());
        var clock = new FakeTimeProvider();
        await using var sut = CreateSender(new(), clock, () => client);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var actual = sut.SendAsync(Message, cancellation.Token);

        // Act
        await cancellation.CancelAsync();
        var act = () => actual;

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        await client.Received(1).SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>(), Arg.Any<ITransferProgress>());
    }

    [Fact]
    public async Task SendAsync_WhenIdle_ShouldDisposeConnection()
    {
        // Arrange
        var client = Substitute.For<ISmtpClient>();
        client.IsConnected.Returns(true);
        var clock = new FakeTimeProvider();
        await using var sut = CreateSender(new() { IdleTimeoutSeconds = 1 }, clock, () => client);
        await sut.SendAsync(Message, TestContext.Current.CancellationToken);

        // Act
        clock.Advance(TimeSpan.FromSeconds(1));
        await Task.Delay(50, TestContext.Current.CancellationToken);

        // Assert
        client.Received(1).Dispose();
    }

    [Fact]
    public async Task SendAsync_WhenRetriesExhausted_ShouldPropagateLastFailure()
    {
        // Arrange
        var failed = Substitute.For<ISmtpClient>();
        failed.IsConnected.Returns(true);
        failed.SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>(), Arg.Any<ITransferProgress>())
            .Returns<Task<string>>(_ => throw new IOException());
        var clock = new FakeTimeProvider();
        await using var sut = CreateSender(new(), clock, () => failed);

        // Act
        var actual = sut.SendAsync(Message, TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromSeconds(3));
        var act = () => actual;

        // Assert
        await act.Should().ThrowAsync<IOException>();
        await failed.Received(2).SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>(), Arg.Any<ITransferProgress>());
    }

    [Fact]
    public async Task SendAsync_WhenCircuitOpen_ShouldNotAttemptSmtp()
    {
        // Arrange
        var options = Options.Create(new SmtpSettings { MaxAttempts = 1, CircuitFailureThreshold = 1 });
        var clock = new FakeTimeProvider();
        var breaker = new SmtpCircuitBreaker(options, clock);
        var client = Substitute.For<ISmtpClient>();
        client.IsConnected.Returns(true);
        client.SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>(), Arg.Any<ITransferProgress>())
            .Returns<Task<string>>(_ => throw new IOException());
        await using var sut = new EmailSender(options, breaker, clock, NullLogger<EmailSender>.Instance, () => client);
        var first = () => sut.SendAsync(Message, TestContext.Current.CancellationToken);
        await first.Should().ThrowAsync<IOException>();

        // Act
        var act = () => sut.SendAsync(Message, TestContext.Current.CancellationToken);

        // Assert
        var actual = await act.Should().ThrowAsync<EmailCircuitOpenException>();
        actual.Which.RetryAfter.Should().Be(clock.GetUtcNow().AddSeconds(30));
        await client.Received(1).SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>(), Arg.Any<ITransferProgress>());
    }

    [Fact]
    public async Task SendAsync_WhenRetryWaiting_ShouldAllowAnotherSend()
    {
        // Arrange
        var failed = Substitute.For<ISmtpClient>();
        var healthy = Substitute.For<ISmtpClient>();
        failed.IsConnected.Returns(true);
        healthy.IsConnected.Returns(true);
        failed.SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>(), Arg.Any<ITransferProgress>())
            .Returns<Task<string>>(_ => throw new IOException());
        var clock = new FakeTimeProvider();
        var clients = new Queue<ISmtpClient>([failed, healthy]);
        await using var sut = CreateSender(new(), clock, clients.Dequeue);

        // Act
        var retrying = sut.SendAsync(Message, TestContext.Current.CancellationToken);
        await sut.SendAsync(Message, TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromSeconds(3));
        await retrying;

        // Assert
        await healthy.Received(2).SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>(), Arg.Any<ITransferProgress>());
    }

    [Fact]
    public async Task SendAsync_WhenConcurrent_ShouldSerializeClientAccess()
    {
        // Arrange
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = Substitute.For<ISmtpClient>();
        client.IsConnected.Returns(true);
        client.SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>(), Arg.Any<ITransferProgress>())
            .Returns(async _ => { entered.TrySetResult(); await release.Task; return "OK"; });
        var clock = new FakeTimeProvider();
        await using var sut = CreateSender(new(), clock, () => client);

        // Act
        var first = sut.SendAsync(Message, TestContext.Current.CancellationToken);
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
        var second = sut.SendAsync(Message, TestContext.Current.CancellationToken);

        // Assert
        await client.Received(1).SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>(), Arg.Any<ITransferProgress>());
        release.SetResult();
        await Task.WhenAll(first, second);
        await client.Received(2).SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>(), Arg.Any<ITransferProgress>());
    }

    [Fact]
    public async Task DisposeAsync_WhenSendInProgress_ShouldCancelAndDisposeClient()
    {
        // Arrange
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = Substitute.For<ISmtpClient>();
        client.IsConnected.Returns(true);
        client.SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>(), Arg.Any<ITransferProgress>())
            .Returns(async call =>
            {
                entered.SetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, call.Arg<CancellationToken>());
                return "OK";
            });
        var sut = CreateSender(new(), new FakeTimeProvider(), () => client);
        var send = sut.SendAsync(Message, TestContext.Current.CancellationToken);
        await entered.Task.WaitAsync(TestContext.Current.CancellationToken);

        // Act
        await sut.DisposeAsync();

        // Assert
        var act = () => send;
        await act.Should().ThrowAsync<OperationCanceledException>();
        client.Received(1).Dispose();
    }

    [Fact]
    public async Task SendAsync_WhenOperationTimesOut_ShouldPropagateTimeout()
    {
        // Arrange
        var client = Substitute.For<ISmtpClient>();
        client.IsConnected.Returns(true);
        client.SendAsync(Arg.Any<MimeMessage>(), Arg.Any<CancellationToken>(), Arg.Any<ITransferProgress>())
            .Returns(async call =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, call.Arg<CancellationToken>());
                return "OK";
            });
        await using var sut = CreateSender(new() { OperationTimeoutSeconds = 1, MaxAttempts = 1 }, new FakeTimeProvider(), () => client);

        // Act
        var act = () => sut.SendAsync(Message, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<TimeoutException>();
    }
}
