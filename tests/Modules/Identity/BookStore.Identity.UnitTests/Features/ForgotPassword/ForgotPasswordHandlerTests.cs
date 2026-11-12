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
        var context = Substitute.For<IIdentityDbContext>();
        var outboxMessages = Substitute.For<DbSet<OutboxMessage>>();
        OutboxMessage? actualMessage = null;
        context.OutboxMessages.Returns(outboxMessages);
        context.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);
        outboxMessages.Add(Arg.Do<OutboxMessage>(message => actualMessage = message));
        var clock = Substitute.For<TimeProvider>();
        clock.GetUtcNow().Returns(now);
        var sut = new ForgotPasswordHandler(
            context,
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
        await context.Received(1).SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
