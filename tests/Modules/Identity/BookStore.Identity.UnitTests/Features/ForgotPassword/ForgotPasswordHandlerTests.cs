namespace BookStore.Identity.UnitTests.Features.ForgotPassword;

public sealed class ForgotPasswordHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenRequestIsValid_ShouldStoreProtectedRecoveryRequest()
    {
        // Arrange
        const string email = "user@example.com";
        var now = new DateTimeOffset(2026, 11, 12, 12, 0, 0, TimeSpan.Zero);
        var lifetime = TimeSpan.FromMinutes(30);
        var protection = new EphemeralDataProtectionProvider();
        var store = Substitute.For<IEmailRequestCooldownStore>();
        OutboxMessage? actualMessage = null;
        store.EnqueueAsync(EmailRequestPurpose.PasswordRecovery, email, Arg.Do<OutboxMessage>(message => actualMessage = message), Arg.Any<CancellationToken>())
            .Returns(new EmailRequestOutcome(true, 60));
        var clock = Substitute.For<TimeProvider>();
        clock.GetUtcNow().Returns(now);
        var sut = new ForgotPasswordHandler(
            store,
            protection,
            Options.Create(new PasswordRecoveryOptions { TokenLifetime = lifetime }),
            clock);

        // Act
        var actual = await sut.HandleAsync(new ForgotPasswordCommand(email), TestContext.Current.CancellationToken);

        // Assert
        actual.Should().BeSuccess();
        actualMessage.Should().NotBeNull();
        actualMessage!.EventType.Should().Be(PasswordRecoveryRequested.EventType);
        actualMessage.CreatedAt.Should().Be(now);
        actualMessage.CorrelationId.Should().Be(actualMessage.Id.ToString());
        var request = JsonSerializer.Deserialize<PasswordRecoveryRequested>(actualMessage.Payload);
        request.Should().NotBeNull();
        protection.CreateProtector(PasswordRecoveryRequested.ProtectionPurpose)
            .Unprotect(request!.ProtectedEmail).Should().Be(email);
        request.ExpiresAt.Should().Be(now + lifetime);
        await store.Received(1).EnqueueAsync(EmailRequestPurpose.PasswordRecovery, email, Arg.Any<OutboxMessage>(), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsync_WhenCooldownIsActive_ShouldReturnStoreOutcome()
    {
        // Arrange
        const string email = "user@example.com";
        var expected = new EmailRequestOutcome(false, 23);
        var store = Substitute.For<IEmailRequestCooldownStore>();
        store.EnqueueAsync(EmailRequestPurpose.PasswordRecovery, email, Arg.Any<OutboxMessage>(), Arg.Any<CancellationToken>()).Returns(expected);
        var sut = new ForgotPasswordHandler(store, new EphemeralDataProtectionProvider(),
            Options.Create(new PasswordRecoveryOptions()), TimeProvider.System);

        // Act
        var actual = await sut.HandleAsync(new ForgotPasswordCommand(email), TestContext.Current.CancellationToken);

        // Assert
        actual.Should().HaveData(value => value.Should().Be(expected));
    }
}
