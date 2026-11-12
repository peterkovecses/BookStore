namespace BookStore.Identity.IntegrationTests;

public class OutboxTransactionTests(IdentityFactory factory) : IClassFixture<IdentityFactory>
{
    [Fact]
    public async Task SaveChanges_WhenUserAndOutboxMessageAreCommitted_ShouldPersistBoth()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await sut.OutboxMessages.ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        var userId = Guid.NewGuid().ToString();
        var eventId = Guid.NewGuid();
        sut.Users.Add(CreateUser(userId));
        sut.OutboxMessages.Add(CreateMessage(eventId));

        // Act
        await sut.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        using var verificationScope = factory.Services.CreateScope();
        var verification = verificationScope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        (await verification.Users.AnyAsync(user => user.Id == userId, TestContext.Current.CancellationToken)).Should().BeTrue();
        (await verification.OutboxMessages.AnyAsync(message => message.Id == eventId, TestContext.Current.CancellationToken)).Should().BeTrue();
    }

    [Fact]
    public async Task SaveChanges_WhenTransactionIsRolledBack_ShouldPersistNeitherUserNorOutboxMessage()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await sut.OutboxMessages.ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        var userId = Guid.NewGuid().ToString();
        var eventId = Guid.NewGuid();
        var control = new OutboxWorkerTests.WorkerControl();
        await using var services = OutboxWorkerTests.CreateServices(sut.Database.GetConnectionString()!, control);
        var worker = ActivatorUtilities.CreateInstance<OutboxWorker<IdentityOutboxStore>>(services);

        // Act
        await sut.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await sut.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
            sut.Users.Add(CreateUser(userId));
            sut.OutboxMessages.Add(CreateMessage(eventId));
            await sut.SaveChangesAsync(TestContext.Current.CancellationToken);
            await transaction.RollbackAsync(TestContext.Current.CancellationToken);
        });
        await worker.StartAsync(TestContext.Current.CancellationToken);
        await Task.Delay(TimeSpan.FromMilliseconds(300), TestContext.Current.CancellationToken);
        await worker.StopAsync(TestContext.Current.CancellationToken);
        worker.Dispose();

        // Assert
        using var verificationScope = factory.Services.CreateScope();
        var verification = verificationScope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        (await verification.Users.AnyAsync(user => user.Id == userId, TestContext.Current.CancellationToken)).Should().BeFalse();
        (await verification.OutboxMessages.AnyAsync(message => message.Id == eventId, TestContext.Current.CancellationToken)).Should().BeFalse();
        control.Calls.Should().Be(0);
    }

    private static ApplicationUser CreateUser(string id) => new()
    {
        Id = id,
        FirstName = "Test",
        LastName = "Producer"
    };

    private static OutboxMessage CreateMessage(Guid id) => new()
    {
        Id = id,
        EventType = "test.event.v1",
        Payload = "{}",
        CorrelationId = id.ToString(),
        CreatedAt = DateTimeOffset.UtcNow
    };
}
