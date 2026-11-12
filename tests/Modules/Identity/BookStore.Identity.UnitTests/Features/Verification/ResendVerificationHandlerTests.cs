namespace BookStore.Identity.UnitTests.Features.Verification;

public sealed class ResendVerificationHandlerTests
{
    [Fact]
    public async Task HandleAsync_WhenRequestIsValid_ShouldStoreProtectedResendRequest()
    {
        // Arrange
        const string email = "user@example.com";
        var now = new DateTimeOffset(2026, 11, 12, 12, 0, 0, TimeSpan.Zero);
        var protection = new EphemeralDataProtectionProvider();
        var store = Substitute.For<IEmailRequestCooldownStore>();
        OutboxMessage? actualMessage = null;
        store.EnqueueAsync(EmailRequestPurpose.Verification, email, Arg.Do<OutboxMessage>(message => actualMessage = message), Arg.Any<CancellationToken>())
            .Returns(new EmailRequestOutcome(true, 60));
        var clock = Substitute.For<TimeProvider>();
        clock.GetUtcNow().Returns(now);
        var sut = new ResendVerificationHandler(store, protection, clock);
        var command = new ResendVerificationCommand(new ResendVerificationRequest { Email = email });

        // Act
        var actual = await sut.HandleAsync(command, TestContext.Current.CancellationToken);

        // Assert
        actual.Should().BeSuccess();
        actualMessage.Should().NotBeNull();
        actualMessage!.EventType.Should().Be(VerificationResendRequested.EventType);
        actualMessage.CreatedAt.Should().Be(now);
        actualMessage.CorrelationId.Should().Be(actualMessage.Id.ToString());
        var request = JsonSerializer.Deserialize<VerificationResendRequested>(actualMessage.Payload);
        request.Should().NotBeNull();
        protection.CreateProtector(VerificationResendRequested.ProtectionPurpose)
            .Unprotect(request!.ProtectedEmail).Should().Be(email);
        await store.Received(1).EnqueueAsync(EmailRequestPurpose.Verification, email, Arg.Any<OutboxMessage>(), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HandleAsync_WhenCooldownIsActive_ShouldReturnStoreOutcome()
    {
        // Arrange
        const string email = "user@example.com";
        var expected = new EmailRequestOutcome(false, 23);
        var store = Substitute.For<IEmailRequestCooldownStore>();
        store.EnqueueAsync(EmailRequestPurpose.Verification, email, Arg.Any<OutboxMessage>(), Arg.Any<CancellationToken>()).Returns(expected);
        var sut = new ResendVerificationHandler(store, new EphemeralDataProtectionProvider(), TimeProvider.System);

        // Act
        var actual = await sut.HandleAsync(new ResendVerificationCommand(new ResendVerificationRequest { Email = email }),
            TestContext.Current.CancellationToken);

        // Assert
        actual.Should().HaveData(value => value.Should().Be(expected));
    }
}
