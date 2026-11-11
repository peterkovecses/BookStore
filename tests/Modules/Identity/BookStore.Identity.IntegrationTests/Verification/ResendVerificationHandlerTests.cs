namespace BookStore.Identity.IntegrationTests.Verification;

public class ResendVerificationHandlerTests(IdentityFactory factory) : IClassFixture<IdentityFactory>
{
    private const string FirstName = "Jane";
    private const string LastName = "Doe";
    private const string Password = "Password1!";

    [Fact]
    public async Task HandleAsync_WhenExistingAccountIsUnconfirmed_ShouldPersistVerificationRequest()
    {
        // Arrange
        const string eventType = "EmailVerificationRequested";
        const string payload = "{\"request\":\"opaque\"}";
        const string correlationId = "verification-correlation";
        var email = $"{Guid.NewGuid()}@example.com";
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var context = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            Email = email,
            UserName = email,
            FirstName = FirstName,
            LastName = LastName
        };
        (await users.CreateAsync(user, Password)).Succeeded.Should().BeTrue();
        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = eventType,
            Payload = payload,
            CorrelationId = correlationId,
            CreatedAt = DateTimeOffset.UnixEpoch,
            Status = OutboxStatus.Pending
        };
        var requestFactory = Substitute.For<IVerificationRequestFactory>();
        requestFactory.CreateAsync(user, Arg.Any<CancellationToken>()).Returns(message);
        var sut = new ResendVerificationHandler(users, requestFactory, context);

        // Act
        var actual = await sut.HandleAsync(
            new ResendVerificationCommand(new ResendVerificationRequest { Email = email }),
            TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();
        var actualMessage = await context.OutboxMessages.SingleAsync(
            item => item.Id == message.Id,
            TestContext.Current.CancellationToken);

        // Assert
        actual.Should().BeSuccess();
        actualMessage.EventType.Should().Be(eventType);
        actualMessage.Payload.Should().Be(payload);
        actualMessage.Status.Should().Be(OutboxStatus.Pending);
        await requestFactory.Received(1).CreateAsync(user, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WhenAccountIsConfirmedOrUnknown_ShouldReturnSameSuccessWithoutQueueing()
    {
        // Arrange
        const string unknownEmail = "unknown-account@example.com";
        var confirmedEmail = $"{Guid.NewGuid()}@example.com";
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var context = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var confirmedUser = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            Email = confirmedEmail,
            UserName = confirmedEmail,
            EmailConfirmed = true,
            FirstName = FirstName,
            LastName = LastName
        };
        (await users.CreateAsync(confirmedUser, Password)).Succeeded.Should().BeTrue();
        var requestFactory = Substitute.For<IVerificationRequestFactory>();
        var sut = new ResendVerificationHandler(users, requestFactory, context);
        var initialMessageCount = await context.OutboxMessages.CountAsync(TestContext.Current.CancellationToken);

        // Act
        var existingAccountResult = await sut.HandleAsync(
            new ResendVerificationCommand(new ResendVerificationRequest { Email = confirmedEmail }),
            TestContext.Current.CancellationToken);
        var unknownAccountResult = await sut.HandleAsync(
            new ResendVerificationCommand(new ResendVerificationRequest { Email = unknownEmail }),
            TestContext.Current.CancellationToken);
        var actualMessageCount = await context.OutboxMessages.CountAsync(TestContext.Current.CancellationToken);

        // Assert
        existingAccountResult.Should().BeSuccess();
        unknownAccountResult.Should().BeSuccess();
        existingAccountResult.Errors.Should().BeNull();
        unknownAccountResult.Errors.Should().BeNull();
        actualMessageCount.Should().Be(initialMessageCount);
        await requestFactory.DidNotReceive().CreateAsync(Arg.Any<ApplicationUser>(), Arg.Any<CancellationToken>());
    }
}
