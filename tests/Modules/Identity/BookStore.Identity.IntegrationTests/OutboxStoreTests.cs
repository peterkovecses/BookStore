namespace BookStore.Identity.IntegrationTests;

[Collection("Identity Integration")]
public class OutboxStoreTests(IdentityFactory factory) : IClassFixture<IdentityFactory>
{
    [Fact]
    public async Task ClaimAsync_WhenTwoWorkersCompete_ShouldClaimEachRecordOnce()
    {
        // Arrange
        const int count = 8;
        using var setup = factory.Services.CreateScope();
        var db = setup.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.OutboxMessages.ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        db.OutboxMessages.AddRange(Enumerable.Range(0, count).Select(_ => Message()));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        using var firstScope = factory.Services.CreateScope();
        using var secondScope = factory.Services.CreateScope();
        var first = firstScope.ServiceProvider.GetRequiredService<IdentityOutboxStore>();
        var second = secondScope.ServiceProvider.GetRequiredService<IdentityOutboxStore>();
        var options = new OutboxOptions();

        // Act
        var actual = await Task.WhenAll(first.ClaimAsync(count, options, TestContext.Current.CancellationToken),
            second.ClaimAsync(count, options, TestContext.Current.CancellationToken));

        // Assert
        actual.SelectMany(claims => claims).Should().HaveCount(count).And.OnlyHaveUniqueItems(claim => claim.Id);
    }

    [Fact]
    public async Task ClaimAsync_WhenLeaseExpires_ShouldFenceStaleOwnerAndRecover()
    {
        // Arrange
        using var setup = factory.Services.CreateScope();
        var db = setup.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.OutboxMessages.ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        var message = Message();
        db.OutboxMessages.Add(message);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        using var firstScope = factory.Services.CreateScope();
        using var secondScope = factory.Services.CreateScope();
        var first = firstScope.ServiceProvider.GetRequiredService<IdentityOutboxStore>();
        var second = secondScope.ServiceProvider.GetRequiredService<IdentityOutboxStore>();
        var options = new OutboxOptions();
        var original = (await first.ClaimAsync(1, options, TestContext.Current.CancellationToken)).Single();
        await db.OutboxMessages.Where(item => item.Id == message.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.LeaseExpiresAt, DateTimeOffset.UtcNow.AddSeconds(-1)),
                TestContext.Current.CancellationToken);

        // Act
        var recovered = (await second.ClaimAsync(1, options, TestContext.Current.CancellationToken)).Single();
        var staleComplete = await first.CompleteAsync(original, TestContext.Current.CancellationToken);
        var staleDefer = await first.DeferAsync(original, DateTimeOffset.UtcNow.AddMinutes(1), TestContext.Current.CancellationToken);
        var completed = await second.CompleteAsync(recovered, TestContext.Current.CancellationToken);

        // Assert
        recovered.Id.Should().Be(original.Id);
        recovered.Token.Should().NotBe(original.Token);
        recovered.AttemptCount.Should().Be(2);
        staleComplete.Should().BeFalse();
        staleDefer.Should().BeFalse();
        completed.Should().BeTrue();
    }

    [Fact]
    public async Task FailAsync_WhenAttemptsExhausted_ShouldAllowReplayWithStableIdentity()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.OutboxMessages.ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        var message = Message();
        db.OutboxMessages.Add(message);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var sut = scope.ServiceProvider.GetRequiredService<IdentityOutboxStore>();
        var options = new OutboxOptions { MaxAttempts = 1 };
        var claim = (await sut.ClaimAsync(1, options, TestContext.Current.CancellationToken)).Single();
        await sut.FailAsync(claim, "TestError", TimeSpan.FromSeconds(1), options.MaxAttempts, TestContext.Current.CancellationToken);

        // Act
        var replayed = await sut.ReplayAsync(message.Id, "test-operator", TestContext.Current.CancellationToken);
        var actual = await db.OutboxMessages.AsNoTracking().SingleAsync(item => item.Id == message.Id, TestContext.Current.CancellationToken);

        // Assert
        replayed.Should().BeTrue();
        actual.Id.Should().Be(claim.Id);
        actual.Status.Should().Be(OutboxStatus.Pending);
        actual.AttemptCount.Should().Be(0);
        actual.CumulativeAttemptCount.Should().Be(1);
        actual.ReplayCount.Should().Be(1);
        actual.ProcessingCycle.Should().Be(1);
        actual.LastReplayedAt.Should().NotBeNull();
        actual.LastReplayedBy.Should().Be("test-operator");
    }

    [Fact]
    public async Task CleanupAsync_WhenRecordsHaveDifferentStates_ShouldDeleteOnlyOldCompleted()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.OutboxMessages.ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        var old = Message();
        old.Status = OutboxStatus.Completed;
        old.CompletedAt = DateTimeOffset.UtcNow.AddDays(-8);
        var recent = Message();
        recent.Status = OutboxStatus.Completed;
        recent.CompletedAt = DateTimeOffset.UtcNow;
        var pending = Message();
        db.OutboxMessages.AddRange(old, recent, pending);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var sut = scope.ServiceProvider.GetRequiredService<IdentityOutboxStore>();

        // Act
        var actual = await sut.CleanupAsync(DateTimeOffset.UtcNow.AddDays(-7), 1, TestContext.Current.CancellationToken);
        var remaining = await db.OutboxMessages.AsNoTracking().Select(item => item.Id).ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        actual.Should().Be(1);
        remaining.Should().BeEquivalentTo([recent.Id, pending.Id]);
    }

    [Fact]
    public async Task ClaimAsync_WhenReadCommittedSnapshotIsEnabled_ShouldAvoidDuplicateClaims()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var databaseName = $"outbox_{Guid.NewGuid():N}";
        await using (var connection = new SqlConnection(db.Database.GetConnectionString()))
        {
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE [{databaseName}]";
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            command.CommandText = $"ALTER DATABASE [{databaseName}] SET READ_COMMITTED_SNAPSHOT ON";
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }
        var connectionString = new SqlConnectionStringBuilder(db.Database.GetConnectionString()) { InitialCatalog = databaseName }.ConnectionString;
        var dbOptions = new DbContextOptionsBuilder<IdentityDbContext>().UseSqlServer(connectionString).Options;
        await using var firstDb = new IdentityDbContext(dbOptions);
        await firstDb.Database.MigrateAsync(TestContext.Current.CancellationToken);
        var message = Message();
        firstDb.OutboxMessages.Add(message);
        await firstDb.SaveChangesAsync(TestContext.Current.CancellationToken);
        await using var secondDb = new IdentityDbContext(dbOptions);
        var first = new IdentityOutboxStore(firstDb, NullLogger<IdentityOutboxStore>.Instance);
        var second = new IdentityOutboxStore(secondDb, NullLogger<IdentityOutboxStore>.Instance);
        var options = new OutboxOptions();

        // Act
        var actual = await Task.WhenAll(first.ClaimAsync(1, options, TestContext.Current.CancellationToken),
            second.ClaimAsync(1, options, TestContext.Current.CancellationToken));

        // Assert
        actual.SelectMany(claims => claims).Should().ContainSingle().Which.Id.Should().Be(message.Id);
    }

    [Fact]
    public async Task ClaimAsync_WhenExpiredClaimHasExhaustedAttempts_ShouldBecomeFailed()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.OutboxMessages.ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        var message = Message();
        message.Status = OutboxStatus.Processing;
        message.AttemptCount = 1;
        message.CumulativeAttemptCount = 1;
        message.ClaimToken = Guid.NewGuid();
        message.LeaseExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1);
        db.OutboxMessages.Add(message);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var sut = scope.ServiceProvider.GetRequiredService<IdentityOutboxStore>();

        // Act
        var claims = await sut.ClaimAsync(1, new OutboxOptions { MaxAttempts = 1 }, TestContext.Current.CancellationToken);
        var actual = await db.OutboxMessages.AsNoTracking().SingleAsync(item => item.Id == message.Id, TestContext.Current.CancellationToken);

        // Assert
        claims.Should().BeEmpty();
        actual.Status.Should().Be(OutboxStatus.Failed);
        actual.AttemptCount.Should().Be(1);
    }

    [Fact]
    public async Task FailAsync_WhenRetryIsDelayed_ShouldPersistAttemptsAcrossStoreInstances()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.OutboxMessages.ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        var message = Message();
        db.OutboxMessages.Add(message);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var first = scope.ServiceProvider.GetRequiredService<IdentityOutboxStore>();
        var options = new OutboxOptions { MaxAttempts = 2 };
        var claim = (await first.ClaimAsync(1, options, TestContext.Current.CancellationToken)).Single();
        await first.FailAsync(claim, "TestFailure", TimeSpan.FromMinutes(1), options.MaxAttempts, TestContext.Current.CancellationToken);
        using var nextScope = factory.Services.CreateScope();
        var second = nextScope.ServiceProvider.GetRequiredService<IdentityOutboxStore>();

        // Act
        var premature = await second.ClaimAsync(1, options, TestContext.Current.CancellationToken);
        await db.OutboxMessages.Where(item => item.Id == message.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.NextAttemptAt, DateTimeOffset.UtcNow.AddSeconds(-1)), TestContext.Current.CancellationToken);
        var retried = (await second.ClaimAsync(1, options, TestContext.Current.CancellationToken)).Single();
        await second.FailAsync(retried, "TestFailure", TimeSpan.FromMinutes(1), options.MaxAttempts, TestContext.Current.CancellationToken);
        var actual = await db.OutboxMessages.AsNoTracking().SingleAsync(item => item.Id == message.Id, TestContext.Current.CancellationToken);

        // Assert
        premature.Should().BeEmpty();
        retried.Id.Should().Be(claim.Id);
        actual.Status.Should().Be(OutboxStatus.Failed);
        actual.AttemptCount.Should().Be(2);
        actual.CumulativeAttemptCount.Should().Be(2);
        actual.LastAttemptAt.Should().NotBeNull();
    }

    [Fact]
    public async Task RenewAsync_WhenClaimIsCurrent_ShouldExtendLeaseAndRejectStaleToken()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.OutboxMessages.ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        var message = Message();
        db.OutboxMessages.Add(message);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var sut = scope.ServiceProvider.GetRequiredService<IdentityOutboxStore>();
        var options = new OutboxOptions();
        var claim = (await sut.ClaimAsync(1, options, TestContext.Current.CancellationToken)).Single();
        var before = await db.OutboxMessages.AsNoTracking().SingleAsync(item => item.Id == message.Id, TestContext.Current.CancellationToken);
        await Task.Delay(20, TestContext.Current.CancellationToken);

        // Act
        var renewed = await sut.RenewAsync(claim, options, TestContext.Current.CancellationToken);
        var stale = await sut.RenewAsync(claim with { Token = Guid.NewGuid() }, options, TestContext.Current.CancellationToken);
        var actual = await db.OutboxMessages.AsNoTracking().SingleAsync(item => item.Id == message.Id, TestContext.Current.CancellationToken);

        // Assert
        renewed.Should().BeTrue();
        stale.Should().BeFalse();
        actual.LeaseExpiresAt.Should().BeAfter(before.LeaseExpiresAt!.Value);
    }

    [Fact]
    public async Task GetDiagnosticsAsync_WhenBacklogExists_ShouldReportStatusCountsAndOldestPending()
    {
        // Arrange
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.OutboxMessages.ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        var pending = Message(DateTimeOffset.UtcNow.AddMinutes(-5));
        var failed = Message();
        failed.Status = OutboxStatus.Failed;
        var expired = Message();
        expired.Status = OutboxStatus.Expired;
        db.OutboxMessages.AddRange(pending, failed, expired);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var sut = scope.ServiceProvider.GetRequiredService<IdentityOutboxStore>();

        // Act
        var actual = await sut.GetDiagnosticsAsync(TestContext.Current.CancellationToken);

        // Assert
        actual.Pending.Should().Be(1);
        actual.Failed.Should().Be(1);
        actual.Expired.Should().Be(1);
        actual.OldestPending.Should().Be(pending.CreatedAt);
    }

    [Fact]
    public async Task CleanupAsync_WhenReplayRacesDeletion_ShouldNeverDeleteReplayedRecord()
    {
        // Arrange
        using var setup = factory.Services.CreateScope();
        var db = setup.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.OutboxMessages.ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        var message = Message();
        message.Status = OutboxStatus.Completed;
        message.CompletedAt = DateTimeOffset.UtcNow.AddDays(-8);
        db.OutboxMessages.Add(message);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        using var replayScope = factory.Services.CreateScope();
        using var cleanupScope = factory.Services.CreateScope();
        var replay = replayScope.ServiceProvider.GetRequiredService<IdentityOutboxStore>();
        var cleanup = cleanupScope.ServiceProvider.GetRequiredService<IdentityOutboxStore>();

        // Act
        var replayTask = replay.ReplayAsync(message.Id, "test-operator", TestContext.Current.CancellationToken);
        var cleanupTask = cleanup.CleanupAsync(DateTimeOffset.UtcNow.AddDays(-7), 1, TestContext.Current.CancellationToken);
        await Task.WhenAll(replayTask, cleanupTask);
        var actual = await db.OutboxMessages.AsNoTracking().SingleOrDefaultAsync(item => item.Id == message.Id, TestContext.Current.CancellationToken);

        // Assert
        (actual is not null).Should().Be(await replayTask);
        (await cleanupTask).Should().Be(await replayTask ? 0 : 1);
        (actual is null || actual.Status == OutboxStatus.Pending).Should().BeTrue();
    }

    private static OutboxMessage Message(DateTimeOffset? createdAt = null) => new()
    {
        Id = Guid.NewGuid(),
        EventType = "test.event.v1",
        Payload = "{}",
        CorrelationId = Guid.NewGuid().ToString(),
        CreatedAt = createdAt ?? DateTimeOffset.UtcNow
    };
}
