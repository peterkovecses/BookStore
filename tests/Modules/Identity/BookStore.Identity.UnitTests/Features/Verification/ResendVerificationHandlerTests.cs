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
        var context = Substitute.For<IIdentityDbContext>();
        var outboxMessages = Substitute.For<DbSet<OutboxMessage>>();
        OutboxMessage? actualMessage = null;
        context.OutboxMessages.Returns(outboxMessages);
        context.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(1);
        outboxMessages.Add(Arg.Do<OutboxMessage>(message => actualMessage = message));
        var clock = Substitute.For<TimeProvider>();
        clock.GetUtcNow().Returns(now);
        var sut = new ResendVerificationHandler(context, protection, clock);
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
        await context.Received(1).SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
