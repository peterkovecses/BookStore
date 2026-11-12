namespace BookStore.Identity.IntegrationTests;

[Collection("Identity Integration")]
public class SecondOutboxTests(IdentityFactory factory) : IClassFixture<IdentityFactory>
{
    [Fact]
    public async Task ExecuteAsync_WhenAnotherModuleOwnsAnOutbox_ShouldProcessOnlyCommittedModuleEvents()
    {
        // Arrange
        using var setup = factory.Services.CreateScope();
        var identity = setup.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await identity.Database.ExecuteSqlRawAsync("""
            IF SCHEMA_ID('testmodule') IS NULL EXEC('CREATE SCHEMA testmodule');
            CREATE TABLE testmodule.BusinessState (Id uniqueidentifier NOT NULL PRIMARY KEY);
            CREATE TABLE testmodule.Outbox (
                Id uniqueidentifier NOT NULL PRIMARY KEY, Payload nvarchar(max) NOT NULL,
                Status int NOT NULL, ClaimToken uniqueidentifier NULL, LeaseExpiresAt datetimeoffset NULL,
                Attempts int NOT NULL, CreatedAt datetimeoffset NOT NULL, CompletedAt datetimeoffset NULL);
            """, TestContext.Current.CancellationToken);
        var connectionString = identity.Database.GetConnectionString()!;
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<SecondContext>(options => options.UseSqlServer(connectionString));
        services.AddDbContext<IdentityDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<IIdentityDbContext>(serviceProvider => serviceProvider.GetRequiredService<IdentityDbContext>());
        services.AddOutbox<SecondStore>(new ConfigurationBuilder().Build());
        services.AddOutbox<IdentityOutboxStore>(new ConfigurationBuilder().Build());
        services.AddRequests(Array.Empty<Assembly>());
        var control = new OutboxWorkerTests.WorkerControl();
        control.Release.TrySetResult();
        services.AddSingleton(control);
        services.AddScoped<IEventHandler<OutboxWorkerTests.TestEvent>, OutboxWorkerTests.TestHandler>();
        services.Configure<OutboxOptions>(options =>
        {
            options.PollInterval = TimeSpan.FromMilliseconds(50);
            options.CleanupInterval = TimeSpan.FromMilliseconds(50);
            options.CleanupBatchSize = 1;
        });
        await using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<OutboxRegistry<SecondStore>>().Register<OutboxWorkerTests.TestEvent>("second.event.v1");
        provider.GetRequiredService<OutboxRegistry<IdentityOutboxStore>>().Register<OutboxWorkerTests.TestEvent>("identity.event.v1");
        var committed = Guid.NewGuid();
        var rolledBack = Guid.NewGuid();
        var identityId = Guid.NewGuid();
        identity.OutboxMessages.Add(new OutboxMessage
        {
            Id = identityId,
            EventType = "identity.event.v1",
            Payload = JsonSerializer.Serialize(new OutboxWorkerTests.TestEvent()),
            CorrelationId = identityId.ToString(),
            CreatedAt = DateTimeOffset.UtcNow
        });
        await identity.SaveChangesAsync(TestContext.Current.CancellationToken);
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SecondContext>();
            db.BusinessStates.Add(new SecondBusiness { Id = committed });
            db.Events.Add(CreateEvent(committed));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            await using var transaction = await db.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
            db.BusinessStates.Add(new SecondBusiness { Id = rolledBack });
            db.Events.Add(CreateEvent(rolledBack));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            await transaction.RollbackAsync(TestContext.Current.CancellationToken);
        }
        var sut = ActivatorUtilities.CreateInstance<OutboxWorker<SecondStore>>(provider);
        var identityWorker = ActivatorUtilities.CreateInstance<OutboxWorker<IdentityOutboxStore>>(provider);

        // Act
        await Task.WhenAll(sut.StartAsync(TestContext.Current.CancellationToken), identityWorker.StartAsync(TestContext.Current.CancellationToken));
        using (var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
        {
            while (true)
            {
                await using var scope = provider.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<SecondContext>();
                if (await db.Events.AnyAsync(item => item.Id == committed && item.Status == 2, deadline.Token)) break;
                await Task.Delay(50, deadline.Token);
            }
        }
        using (var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
        {
            while (!await identity.OutboxMessages.AsNoTracking().AnyAsync(item => item.Id == identityId && item.Status == OutboxStatus.Completed, deadline.Token))
                await Task.Delay(50, deadline.Token);
        }
        await Task.WhenAll(sut.StopAsync(TestContext.Current.CancellationToken), identityWorker.StopAsync(TestContext.Current.CancellationToken));
        await using var verifyScope = provider.CreateAsyncScope();
        var verification = verifyScope.ServiceProvider.GetRequiredService<SecondContext>();
        var states = await verification.BusinessStates.Select(item => item.Id).ToListAsync(TestContext.Current.CancellationToken);
        var events = await verification.Events.Select(item => item.Id).ToListAsync(TestContext.Current.CancellationToken);
        var identityCount = await identity.OutboxMessages.CountAsync(TestContext.Current.CancellationToken);
        sut.Dispose();
        identityWorker.Dispose();
        await verification.Events.Where(item => item.Id == committed)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.CompletedAt, DateTimeOffset.UtcNow.AddDays(-8)), TestContext.Current.CancellationToken);
        var cleaner = ActivatorUtilities.CreateInstance<OutboxCleaner<SecondStore>>(provider);
        await cleaner.StartAsync(TestContext.Current.CancellationToken);
        using (var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
        {
            while (await verification.Events.AnyAsync(item => item.Id == committed, deadline.Token))
                await Task.Delay(50, deadline.Token);
        }
        await cleaner.StopAsync(TestContext.Current.CancellationToken);
        cleaner.Dispose();

        // Assert
        states.Should().Equal(committed);
        events.Should().Equal(committed);
        identityCount.Should().Be(1);
        control.Calls.Should().Be(2);
    }

    private static SecondMessage CreateEvent(Guid id) => new()
    {
        Id = id,
        Payload = JsonSerializer.Serialize(new OutboxWorkerTests.TestEvent()),
        CreatedAt = DateTimeOffset.UtcNow
    };

    public sealed class SecondBusiness { public Guid Id { get; init; } }
    public sealed class SecondMessage
    {
        public Guid Id { get; init; }
        public required string Payload { get; init; }
        public int Status { get; set; }
        public Guid? ClaimToken { get; set; }
        public DateTimeOffset? LeaseExpiresAt { get; set; }
        public int Attempts { get; set; }
        public DateTimeOffset CreatedAt { get; init; }
        public DateTimeOffset? CompletedAt { get; set; }
    }

    public sealed class SecondContext(DbContextOptions<SecondContext> options) : DbContext(options)
    {
        public DbSet<SecondBusiness> BusinessStates => Set<SecondBusiness>();
        public DbSet<SecondMessage> Events => Set<SecondMessage>();
        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.Entity<SecondBusiness>().ToTable("BusinessState", "testmodule");
            builder.Entity<SecondMessage>().ToTable("Outbox", "testmodule");
        }
    }

    public sealed class SecondStore(SecondContext db) : IOutboxStore
    {
        public async Task<IReadOnlyList<OutboxClaim>> ClaimAsync(int count, OutboxOptions options, CancellationToken cancellationToken)
        {
            var claims = new List<OutboxClaim>();
            var candidates = await db.Events.AsNoTracking().Where(item => item.Status == 0)
                .OrderBy(item => item.CreatedAt).Take(count).ToListAsync(cancellationToken);
            foreach (var item in candidates)
            {
                var token = Guid.NewGuid();
                var expires = DateTimeOffset.UtcNow + options.LeaseDuration;
                var changed = await db.Events.Where(row => row.Id == item.Id && row.Status == 0)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.Status, 1)
                        .SetProperty(row => row.ClaimToken, token).SetProperty(row => row.LeaseExpiresAt, expires)
                        .SetProperty(row => row.Attempts, row => row.Attempts + 1), cancellationToken);
                if (changed == 1) claims.Add(new(item.Id, "second.event.v1", item.Payload, item.Id.ToString(), token, item.Attempts + 1));
            }

            return claims;
        }

        private IQueryable<SecondMessage> Owned(OutboxClaim claim) =>
            db.Events.Where(row => row.Id == claim.Id && row.Status == 1 && row.ClaimToken == claim.Token && row.LeaseExpiresAt > DateTimeOffset.UtcNow);

        public async Task<bool> CompleteAsync(OutboxClaim claim, CancellationToken cancellationToken) =>
            await Owned(claim).ExecuteUpdateAsync(setters => setters.SetProperty(row => row.Status, 2)
                .SetProperty(row => row.CompletedAt, DateTimeOffset.UtcNow), cancellationToken) == 1;
        public async Task<bool> RenewAsync(OutboxClaim claim, OutboxOptions options, CancellationToken cancellationToken) =>
            await Owned(claim).ExecuteUpdateAsync(setters => setters.SetProperty(row => row.LeaseExpiresAt, DateTimeOffset.UtcNow + options.LeaseDuration), cancellationToken) == 1;
        public async Task<bool> FailAsync(OutboxClaim claim, string error, TimeSpan delay, int maxAttempts, CancellationToken cancellationToken) =>
            await Owned(claim).ExecuteUpdateAsync(setters => setters.SetProperty(row => row.Status, 3), cancellationToken) == 1;
        public async Task<bool> ExpireAsync(OutboxClaim claim, CancellationToken cancellationToken) =>
            await Owned(claim).ExecuteUpdateAsync(setters => setters.SetProperty(row => row.Status, 4), cancellationToken) == 1;
        public async Task<bool> DeferAsync(OutboxClaim claim, DateTimeOffset nextAttempt, CancellationToken cancellationToken) =>
            await Owned(claim).ExecuteUpdateAsync(setters => setters.SetProperty(row => row.Status, 0)
                .SetProperty(row => row.Attempts, row => row.Attempts - 1), cancellationToken) == 1;
        public Task<int> CleanupAsync(DateTimeOffset cutoff, int batchSize, CancellationToken cancellationToken) =>
            db.Events.Where(row => row.Status == 2 && row.CompletedAt < cutoff).Take(batchSize).ExecuteDeleteAsync(cancellationToken);
        public Task<OutboxDiagnostics> GetDiagnosticsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new OutboxDiagnostics(0, 0, 0, 0, null));
    }
}
