namespace BookStore.Identity.IntegrationTests.Verification;

public class ResendVerificationHandlerTests(IdentityFactory factory) : IClassFixture<IdentityFactory>
{
    [Fact]
    public async Task HandleAsync_WhenAccountStatesDiffer_ShouldQueueSameProtectedRequestForEach()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var context = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var protection = scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>();
        var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();
        var unknownEmail = $"{Guid.NewGuid():N}@example.com";
        var confirmed = CreateUser(true);
        var unconfirmed = CreateUser(false);
        (await users.CreateAsync(confirmed, "Password1!")).Succeeded.Should().BeTrue();
        (await users.CreateAsync(unconfirmed, "Password1!")).Succeeded.Should().BeTrue();
        var emails = new[] { unknownEmail, confirmed.Email!, unconfirmed.Email! };
        var sut = new ResendVerificationHandler(context, protection, clock);
        var initialIds = await context.OutboxMessages.Select(item => item.Id).ToListAsync(TestContext.Current.CancellationToken);

        // Act
        var results = new List<Kovecses.Result.Result>();
        foreach (var email in emails)
        {
            results.Add(await sut.HandleAsync(new ResendVerificationCommand(new ResendVerificationRequest { Email = email }),
                TestContext.Current.CancellationToken));
        }
        var messages = await context.OutboxMessages.AsNoTracking().Where(item => !initialIds.Contains(item.Id))
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        results.Should().AllSatisfy(result => result.Should().BeSuccess());
        messages.Should().HaveCount(3).And.OnlyContain(message =>
            message.EventType == VerificationResendRequested.EventType && message.Status == OutboxStatus.Pending);
        var decoded = messages.Select(message =>
        {
            var value = JsonSerializer.Deserialize<VerificationResendRequested>(message.Payload)!;

            return protection.CreateProtector(VerificationResendRequested.ProtectionPurpose).Unprotect(value.ProtectedEmail);
        });
        decoded.Should().BeEquivalentTo(emails);
        foreach (var message in messages)
            message.Payload.Should().NotContainAny(emails);
    }

    [Fact]
    public async Task PublishAsync_WhenAccountStatesDiffer_ShouldQueueEmailOnlyForUnconfirmedUser()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var context = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var protection = scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>();
        var confirmed = CreateUser(true);
        var unconfirmed = CreateUser(false);
        (await users.CreateAsync(confirmed, "Password1!")).Succeeded.Should().BeTrue();
        (await users.CreateAsync(unconfirmed, "Password1!")).Succeeded.Should().BeTrue();
        var unknownEmail = $"{Guid.NewGuid():N}@example.com";
        var initialIds = await context.OutboxMessages.Select(item => item.Id).ToListAsync(TestContext.Current.CancellationToken);
        var handlers = scope.ServiceProvider.GetServices<IEventHandler<VerificationResendRequested>>().ToList();
        var sut = scope.ServiceProvider.GetRequiredService<IEventPublisher<VerificationResendRequested>>();
        VerificationResendRequested Request(string email) =>
            new(protection.CreateProtector(VerificationResendRequested.ProtectionPurpose).Protect(email));

        // Act
        await sut.PublishAsync(Request(unknownEmail), TestContext.Current.CancellationToken);
        await sut.PublishAsync(Request(confirmed.Email!), TestContext.Current.CancellationToken);
        await sut.PublishAsync(Request(unconfirmed.Email!), TestContext.Current.CancellationToken);
        var messages = await context.OutboxMessages.AsNoTracking().Where(item => !initialIds.Contains(item.Id))
            .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        handlers.Should().ContainSingle();
        messages.Should().ContainSingle();
        messages[0].EventType.Should().Be(VerificationRequestFactory.EventType);
        var value = JsonSerializer.Deserialize<VerificationEmailRequested>(messages[0].Payload);
        value!.UserId.Should().Be(unconfirmed.Id);
        value.Email.Should().Be(unconfirmed.Email);
    }

    private static ApplicationUser CreateUser(bool confirmed) => new()
    {
        Id = Guid.NewGuid().ToString(),
        Email = $"{Guid.NewGuid():N}@example.com",
        UserName = $"{Guid.NewGuid():N}@example.com",
        EmailConfirmed = confirmed,
        FirstName = "Jane",
        LastName = "Doe"
    };
}
