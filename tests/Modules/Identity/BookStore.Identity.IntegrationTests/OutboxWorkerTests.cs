namespace BookStore.Identity.IntegrationTests;

[Collection("Outbox integration")]
public class OutboxWorkerTests(IdentityFactory factory) : IClassFixture<IdentityFactory>
{
    [Fact]
    public async Task ExecuteAsync_WhenTwoWorkersCompete_ShouldInvokeHandlerOnceDuringHealthyLease()
    {
        // Arrange
        using var setup = factory.Services.CreateScope();
        var db = setup.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.OutboxMessages.ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        var message = Message();
        db.OutboxMessages.Add(message);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var control = new WorkerControl();
        await using var services = CreateServices(db.Database.GetConnectionString()!, control);
        var first = ActivatorUtilities.CreateInstance<OutboxWorker<IdentityOutboxStore>>(services);
        var second = ActivatorUtilities.CreateInstance<OutboxWorker<IdentityOutboxStore>>(services);

        // Act
        await Task.WhenAll(first.StartAsync(TestContext.Current.CancellationToken), second.StartAsync(TestContext.Current.CancellationToken));
        await control.Started.Task.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);
        await Task.Delay(TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken);
        var callsWhileBlocked = control.Calls;
        control.Release.TrySetResult();
        var completed = await WaitForStatusAsync(message.Id, OutboxStatus.Completed);
        await Task.WhenAll(first.StopAsync(TestContext.Current.CancellationToken), second.StopAsync(TestContext.Current.CancellationToken));
        first.Dispose();
        second.Dispose();

        // Assert
        callsWhileBlocked.Should().Be(1);
        control.Calls.Should().Be(1);
        control.Logs.Should().Contain(entry => entry.Contains(message.Id.ToString()) && entry.Contains(message.CorrelationId));
        control.Logs.Should().Contain(entry => entry.Contains("Outbox completed", StringComparison.Ordinal));
        control.Logs.Should().Contain(entry => entry.Contains("drained all handlers", StringComparison.Ordinal));
        control.Logs.Should().NotContain(entry => entry.Contains(message.Payload));
        control.Logger.Properties.Should().Contain(entry =>
            entry.Any(pair => pair.Key == "EventId" && Equals(pair.Value, message.Id)) &&
            entry.Any(pair => pair.Key == "CorrelationId" && Equals(pair.Value, message.CorrelationId)));
        control.Logger.Properties.Should().Contain(entry =>
            entry.Any(pair => pair.Key == "EventId" && Equals(pair.Value, completed.Id)) &&
            entry.Any(pair => pair.Key == "Attempt" && Equals(pair.Value, completed.AttemptCount)));
    }

    [Fact]
    public async Task ExecuteAsync_WhenWorkersCompeteUnderReadCommittedSnapshot_ShouldInvokeHandlerOnce()
    {
        // Arrange
        using var setup = factory.Services.CreateScope();
        var db = setup.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var databaseName = $"outbox_worker_{Guid.NewGuid():N}";
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
        await using var isolated = new IdentityDbContext(dbOptions);
        await isolated.Database.MigrateAsync(TestContext.Current.CancellationToken);
        var message = Message();
        isolated.OutboxMessages.Add(message);
        await isolated.SaveChangesAsync(TestContext.Current.CancellationToken);
        var control = new WorkerControl();
        await using var services = CreateServices(connectionString, control);
        var first = ActivatorUtilities.CreateInstance<OutboxWorker<IdentityOutboxStore>>(services);
        var second = ActivatorUtilities.CreateInstance<OutboxWorker<IdentityOutboxStore>>(services);

        // Act
        await Task.WhenAll(first.StartAsync(TestContext.Current.CancellationToken), second.StartAsync(TestContext.Current.CancellationToken));
        await control.Started.Task.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);
        await Task.Delay(TimeSpan.FromMilliseconds(500), TestContext.Current.CancellationToken);
        var callsWhileBlocked = control.Calls;
        control.Release.TrySetResult();
        using (var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
        {
            while (!await isolated.OutboxMessages.AsNoTracking().AnyAsync(item => item.Id == message.Id && item.Status == OutboxStatus.Completed, deadline.Token))
                await Task.Delay(50, deadline.Token);
        }
        await Task.WhenAll(first.StopAsync(TestContext.Current.CancellationToken), second.StopAsync(TestContext.Current.CancellationToken));
        first.Dispose();
        second.Dispose();

        // Assert
        callsWhileBlocked.Should().Be(1);
        control.Calls.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoHandlerExists_ShouldFailInsteadOfCompleting()
    {
        // Arrange
        using var setup = factory.Services.CreateScope();
        var db = setup.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.OutboxMessages.ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        var message = Message();
        db.OutboxMessages.Add(message);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var control = new WorkerControl();
        await using var services = CreateServices(db.Database.GetConnectionString()!, control, registerHandler: false);
        var sut = ActivatorUtilities.CreateInstance<OutboxWorker<IdentityOutboxStore>>(services);

        // Act
        await sut.StartAsync(TestContext.Current.CancellationToken);
        var actual = await WaitForStatusAsync(message.Id, OutboxStatus.Failed);
        await sut.StopAsync(TestContext.Current.CancellationToken);
        sut.Dispose();

        // Assert
        actual.AttemptCount.Should().Be(1);
        actual.LastError.Should().Be(nameof(NoEventSubscribersException));
        control.Logs.Should().Contain(entry => entry.Contains("terminal failure", StringComparison.Ordinal) &&
            entry.Contains(nameof(NoEventSubscribersException), StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_WhenPreflightDefersOneEvent_ShouldNotConsumeAttemptOrBlockAnother()
    {
        // Arrange
        using var setup = factory.Services.CreateScope();
        var db = setup.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.OutboxMessages.ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        var deferred = Message(new TestEvent(Defer: true));
        var ready = Message(new TestEvent(Slow: false));
        db.OutboxMessages.AddRange(deferred, ready);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var control = new WorkerControl();
        await using var services = CreateServices(db.Database.GetConnectionString()!, control, preflight: true);
        var sut = ActivatorUtilities.CreateInstance<OutboxWorker<IdentityOutboxStore>>(services);

        // Act
        await sut.StartAsync(TestContext.Current.CancellationToken);
        var completed = await WaitForStatusAsync(ready.Id, OutboxStatus.Completed);
        OutboxMessage actual;
        using (var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
        {
            do
            {
                await Task.Delay(50, deadline.Token);
                actual = await db.OutboxMessages.AsNoTracking().SingleAsync(item => item.Id == deferred.Id, deadline.Token);
            } while (actual.NextAttemptAt is null);
        }
        await sut.StopAsync(TestContext.Current.CancellationToken);
        sut.Dispose();

        // Assert
        completed.Status.Should().Be(OutboxStatus.Completed);
        actual.Status.Should().Be(OutboxStatus.Pending);
        actual.AttemptCount.Should().Be(0);
        actual.CumulativeAttemptCount.Should().Be(0);
        actual.NextAttemptAt.Should().BeAfter(DateTimeOffset.UtcNow.AddSeconds(5));
        control.Calls.Should().Be(1);
        control.Logger.Properties.Should().Contain(entry =>
            entry.Any(pair => pair.Key == "NextAttemptAt" && pair.Value is DateTimeOffset));
    }

    [Fact]
    public async Task ExecuteAsync_WhenSmtpCircuitIsOpen_ShouldDeferOnlyDependentEvent()
    {
        // Arrange
        using var setup = factory.Services.CreateScope();
        var db = setup.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.OutboxMessages.ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        var dependent = Message(new TestEvent(Slow: false));
        var unrelated = Message(eventType: "independent.event.v1",
            payload: JsonSerializer.Serialize(new IndependentEvent()));
        db.OutboxMessages.AddRange(dependent, unrelated);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var control = new WorkerControl();
        var recovery = DateTimeOffset.UtcNow.AddSeconds(10);
        var availability = Substitute.For<IEmailDispatchAvailability>();
        availability.GetAvailability().Returns(new EmailDispatchAvailability(false, recovery));
        await using var services = CreateServices(db.Database.GetConnectionString()!, control, smtpAvailability: availability);
        var sut = ActivatorUtilities.CreateInstance<OutboxWorker<IdentityOutboxStore>>(services);

        // Act
        await sut.StartAsync(TestContext.Current.CancellationToken);
        var completed = await WaitForStatusAsync(unrelated.Id, OutboxStatus.Completed);
        OutboxMessage actual;
        using (var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
        {
            do
            {
                await Task.Delay(50, deadline.Token);
                actual = await db.OutboxMessages.AsNoTracking().SingleAsync(item => item.Id == dependent.Id, deadline.Token);
            } while (actual.NextAttemptAt is null);
        }
        await sut.StopAsync(TestContext.Current.CancellationToken);
        sut.Dispose();

        // Assert
        completed.Status.Should().Be(OutboxStatus.Completed);
        actual.AttemptCount.Should().Be(0);
        actual.NextAttemptAt.Should().BeOnOrAfter(recovery);
        control.Calls.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSmtpOutagePersists_ShouldRepeatedlyDeferWithoutConsumingAttempts()
    {
        // Arrange
        using var setup = factory.Services.CreateScope();
        var db = setup.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.OutboxMessages.ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        var message = Message(new TestEvent(Slow: false));
        db.OutboxMessages.Add(message);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var control = new WorkerControl();
        var availability = Substitute.For<IEmailDispatchAvailability>();
        availability.GetAvailability().Returns(new EmailDispatchAvailability(false, null));
        await using var services = CreateServices(db.Database.GetConnectionString()!, control,
            smtpAvailability: availability, fallbackDelay: TimeSpan.FromMilliseconds(100));
        var sut = ActivatorUtilities.CreateInstance<OutboxWorker<IdentityOutboxStore>>(services);

        // Act
        await sut.StartAsync(TestContext.Current.CancellationToken);
        using (var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
        {
            while (control.Logs.Count(entry => entry.Contains("preflight deferred", StringComparison.Ordinal)) < 3)
                await Task.Delay(50, deadline.Token);
        }
        await sut.StopAsync(TestContext.Current.CancellationToken);
        var actual = await db.OutboxMessages.AsNoTracking().SingleAsync(item => item.Id == message.Id, TestContext.Current.CancellationToken);
        sut.Dispose();

        // Assert
        actual.Status.Should().Be(OutboxStatus.Pending);
        actual.AttemptCount.Should().Be(0);
        actual.CumulativeAttemptCount.Should().Be(0);
        control.Calls.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_WhenOneHandlerIsSlow_ShouldCompleteIndependentEvent()
    {
        // Arrange
        using var setup = factory.Services.CreateScope();
        var db = setup.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.OutboxMessages.ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        var slow = Message();
        var fast = Message(new TestEvent(Slow: false));
        db.OutboxMessages.AddRange(slow, fast);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var control = new WorkerControl();
        await using var services = CreateServices(db.Database.GetConnectionString()!, control);
        var sut = ActivatorUtilities.CreateInstance<OutboxWorker<IdentityOutboxStore>>(services);

        // Act
        await sut.StartAsync(TestContext.Current.CancellationToken);
        await control.Started.Task.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);
        var actual = await WaitForStatusAsync(fast.Id, OutboxStatus.Completed);
        var stillProcessing = await db.OutboxMessages.AsNoTracking().SingleAsync(item => item.Id == slow.Id, TestContext.Current.CancellationToken);
        control.Release.TrySetResult();
        await WaitForStatusAsync(slow.Id, OutboxStatus.Completed);
        await sut.StopAsync(TestContext.Current.CancellationToken);
        sut.Dispose();

        // Assert
        actual.Status.Should().Be(OutboxStatus.Completed);
        stillProcessing.Status.Should().Be(OutboxStatus.Processing);
        control.ScopeIds.Should().OnlyHaveUniqueItems().And.HaveCount(2);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTwoCleanersRun_ShouldRemoveOnlyOldCompletedRows()
    {
        // Arrange
        using var setup = factory.Services.CreateScope();
        var db = setup.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.OutboxMessages.ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        var old = Message();
        old.Status = OutboxStatus.Completed;
        old.CompletedAt = DateTimeOffset.UtcNow.AddDays(-8);
        var recent = Message();
        recent.Status = OutboxStatus.Completed;
        recent.CompletedAt = DateTimeOffset.UtcNow;
        var otherStates = new[] { OutboxStatus.Pending, OutboxStatus.Processing, OutboxStatus.Failed, OutboxStatus.Expired }
            .Select(status =>
            {
                var item = Message();
                item.Status = status;

                return item;
            }).ToArray();
        db.OutboxMessages.AddRange([old, recent, .. otherStates]);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        await using var services = CreateServices(db.Database.GetConnectionString()!, new WorkerControl());
        var first = ActivatorUtilities.CreateInstance<OutboxCleaner<IdentityOutboxStore>>(services);
        var second = ActivatorUtilities.CreateInstance<OutboxCleaner<IdentityOutboxStore>>(services);

        // Act
        await Task.WhenAll(first.StartAsync(TestContext.Current.CancellationToken), second.StartAsync(TestContext.Current.CancellationToken));
        using (var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
        {
            while (await db.OutboxMessages.AsNoTracking().AnyAsync(item => item.Id == old.Id, deadline.Token))
                await Task.Delay(50, deadline.Token);
        }
        await Task.WhenAll(first.StopAsync(TestContext.Current.CancellationToken), second.StopAsync(TestContext.Current.CancellationToken));
        var actual = await db.OutboxMessages.AsNoTracking().Select(item => item.Id).ToListAsync(TestContext.Current.CancellationToken);
        first.Dispose();
        second.Dispose();

        // Assert
        actual.Should().BeEquivalentTo(otherStates.Select(item => item.Id).Append(recent.Id));
    }

    [Fact]
    public async Task ExecuteAsync_WhenCleanupFailsOnce_ShouldLogAndDeleteOnNextRun()
    {
        // Arrange
        using var setup = factory.Services.CreateScope();
        var db = setup.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.OutboxMessages.ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        var old = Message();
        old.Status = OutboxStatus.Completed;
        old.CompletedAt = DateTimeOffset.UtcNow.AddDays(-8);
        db.OutboxMessages.Add(old);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var control = new WorkerControl();
        await using var services = CreateServices(db.Database.GetConnectionString()!, control);
        var sut = ActivatorUtilities.CreateInstance<OutboxCleaner<FaultingStore>>(services);

        // Act
        await sut.StartAsync(TestContext.Current.CancellationToken);
        using (var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
        {
            while (await db.OutboxMessages.AsNoTracking().AnyAsync(item => item.Id == old.Id, deadline.Token))
                await Task.Delay(50, deadline.Token);
        }
        await sut.StopAsync(TestContext.Current.CancellationToken);
        sut.Dispose();

        // Assert
        control.Logs.Should().Contain(entry => entry.Contains("Outbox cleanup error", StringComparison.Ordinal));
        control.CleanupCalls.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCircuitOpensAfterFirstHandler_ShouldRetryWholeEventWithinBudget()
    {
        // Arrange
        using var setup = factory.Services.CreateScope();
        var db = setup.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.OutboxMessages.ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        var message = Message(new TestEvent(Slow: false));
        db.OutboxMessages.Add(message);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var control = new WorkerControl();
        var sender = Substitute.For<IEmailSender>();
        await using var services = CreateServices(db.Database.GetConnectionString()!, control, preflight: true,
            throwAfterHandler: true, retry: true, sender: sender);
        var sut = ActivatorUtilities.CreateInstance<OutboxWorker<IdentityOutboxStore>>(services);

        // Act
        await sut.StartAsync(TestContext.Current.CancellationToken);
        var actual = await WaitForStatusAsync(message.Id, OutboxStatus.Failed);
        await sut.StopAsync(TestContext.Current.CancellationToken);
        sut.Dispose();

        // Assert
        actual.Id.Should().Be(message.Id);
        actual.AttemptCount.Should().Be(2);
        actual.CumulativeAttemptCount.Should().Be(2);
        control.EventIds.Should().HaveCount(2).And.OnlyContain(id => id == message.Id);
        actual.LastError.Should().Be(nameof(EmailCircuitOpenException));
        control.Calls.Should().Be(2);
        await sender.Received(2).SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenHandlerIgnoresTimeout_ShouldNotCompleteOrReleaseUntilLeaseExpires()
    {
        // Arrange
        using var setup = factory.Services.CreateScope();
        var db = setup.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.OutboxMessages.ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        var message = Message();
        db.OutboxMessages.Add(message);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var control = new WorkerControl { IgnoreCancellation = true };
        await using var services = CreateServices(db.Database.GetConnectionString()!, control, retry: true, shortTimeout: true);
        var first = ActivatorUtilities.CreateInstance<OutboxWorker<IdentityOutboxStore>>(services);
        var second = ActivatorUtilities.CreateInstance<OutboxWorker<IdentityOutboxStore>>(services);

        // Act
        await first.StartAsync(TestContext.Current.CancellationToken);
        await control.Started.Task.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);
        await Task.Delay(TimeSpan.FromSeconds(1), TestContext.Current.CancellationToken);
        var duringTimeout = await db.OutboxMessages.AsNoTracking().SingleAsync(item => item.Id == message.Id, TestContext.Current.CancellationToken);
        control.Release.TrySetResult();
        await first.StopAsync(TestContext.Current.CancellationToken);
        await db.OutboxMessages.Where(item => item.Id == message.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.LeaseExpiresAt, DateTimeOffset.UtcNow.AddSeconds(-1)), TestContext.Current.CancellationToken);
        await second.StartAsync(TestContext.Current.CancellationToken);
        var actual = await WaitForStatusAsync(message.Id, OutboxStatus.Completed);
        await second.StopAsync(TestContext.Current.CancellationToken);
        first.Dispose();
        second.Dispose();

        // Assert
        duringTimeout.Status.Should().Be(OutboxStatus.Processing);
        actual.AttemptCount.Should().Be(2);
        control.Calls.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteAsync_WhenHandlerHonorsTimeout_ShouldRecordBoundedFailure()
    {
        // Arrange
        using var setup = factory.Services.CreateScope();
        var db = setup.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.OutboxMessages.ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        var message = Message();
        db.OutboxMessages.Add(message);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var control = new WorkerControl();
        await using var services = CreateServices(db.Database.GetConnectionString()!, control, shortTimeout: true);
        var sut = ActivatorUtilities.CreateInstance<OutboxWorker<IdentityOutboxStore>>(services);

        // Act
        await sut.StartAsync(TestContext.Current.CancellationToken);
        var actual = await WaitForStatusAsync(message.Id, OutboxStatus.Failed);
        await sut.StopAsync(TestContext.Current.CancellationToken);
        sut.Dispose();

        // Assert
        actual.AttemptCount.Should().Be(1);
        actual.LastError.Should().Be(nameof(TimeoutException));
        actual.ClaimToken.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_WhenShutdownDrainTimesOut_ShouldNotLogSuccessfulDrainOrReleaseClaim()
    {
        // Arrange
        using var setup = factory.Services.CreateScope();
        var db = setup.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.OutboxMessages.ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        var message = Message();
        db.OutboxMessages.Add(message);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var control = new WorkerControl { IgnoreCancellation = true };
        await using var services = CreateServices(db.Database.GetConnectionString()!, control, shortShutdown: true);
        var sut = ActivatorUtilities.CreateInstance<OutboxWorker<IdentityOutboxStore>>(services);

        // Act
        await sut.StartAsync(TestContext.Current.CancellationToken);
        await control.Started.Task.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);
        await sut.StopAsync(TestContext.Current.CancellationToken);
        var duringDrain = await db.OutboxMessages.AsNoTracking()
            .SingleAsync(item => item.Id == message.Id, TestContext.Current.CancellationToken);
        control.Release.TrySetResult();
        await control.Finished.Task.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);
        sut.Dispose();

        // Assert
        duringDrain.Status.Should().Be(OutboxStatus.Processing);
        duringDrain.ClaimToken.Should().NotBeNull();
        control.Logs.Should().Contain(entry => entry.Contains("drain timed out", StringComparison.Ordinal));
        control.Logs.Should().NotContain(entry => entry.Contains("drained all handlers", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("unknown.event.v1", "{}")]
    [InlineData("test.event.v1", "not-json")]
    public async Task ExecuteAsync_WhenEventCannotBeResolved_ShouldFail(string eventType, string payload)
    {
        // Arrange
        using var setup = factory.Services.CreateScope();
        var db = setup.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.OutboxMessages.ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        var message = Message(eventType: eventType, payload: payload);
        db.OutboxMessages.Add(message);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var control = new WorkerControl();
        await using var services = CreateServices(db.Database.GetConnectionString()!, control);
        var sut = ActivatorUtilities.CreateInstance<OutboxWorker<IdentityOutboxStore>>(services);

        // Act
        await sut.StartAsync(TestContext.Current.CancellationToken);
        var actual = await WaitForStatusAsync(message.Id, OutboxStatus.Failed);
        await sut.StopAsync(TestContext.Current.CancellationToken);
        sut.Dispose();

        // Assert
        actual.AttemptCount.Should().Be(1);
        control.Calls.Should().Be(0);
        control.Logs.Should().Contain(entry => entry.Contains("terminal failure", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_WhenHandlerResolutionFails_ShouldNotLeakExceptionOrPayload()
    {
        // Arrange
        const string secret = "sensitive-test-token";
        using var setup = factory.Services.CreateScope();
        var db = setup.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.OutboxMessages.ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        var message = Message(payload: $$"""{"Slow":false,"secret":"{{secret}}"}""");
        db.OutboxMessages.Add(message);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var control = new WorkerControl();
        await using var services = CreateServices(db.Database.GetConnectionString()!, control, brokenDependency: true);
        var sut = ActivatorUtilities.CreateInstance<OutboxWorker<IdentityOutboxStore>>(services);

        // Act
        await sut.StartAsync(TestContext.Current.CancellationToken);
        var actual = await WaitForStatusAsync(message.Id, OutboxStatus.Failed);
        await sut.StopAsync(TestContext.Current.CancellationToken);
        sut.Dispose();

        // Assert
        actual.LastError.Should().Be(nameof(InvalidOperationException));
        actual.LastError.Should().NotContain(secret);
        control.Logs.Should().NotContain(entry => entry.Contains(secret, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_WhenEventHasExpired_ShouldNotInvokeHandlerOrRetry()
    {
        // Arrange
        using var setup = factory.Services.CreateScope();
        var db = setup.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.OutboxMessages.ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        var message = Message(new TestEvent(Expired: true));
        db.OutboxMessages.Add(message);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var control = new WorkerControl();
        await using var services = CreateServices(db.Database.GetConnectionString()!, control, expiry: true);
        var sut = ActivatorUtilities.CreateInstance<OutboxWorker<IdentityOutboxStore>>(services);

        // Act
        await sut.StartAsync(TestContext.Current.CancellationToken);
        var actual = await WaitForStatusAsync(message.Id, OutboxStatus.Expired);
        await sut.StopAsync(TestContext.Current.CancellationToken);
        sut.Dispose();

        // Assert
        actual.AttemptCount.Should().Be(1);
        control.Calls.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSeveralHandlersExist_ShouldWaitForEveryHandler()
    {
        // Arrange
        using var setup = factory.Services.CreateScope();
        var db = setup.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.OutboxMessages.ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        var message = Message(new TestEvent(Slow: false));
        db.OutboxMessages.Add(message);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var control = new WorkerControl();
        await using var services = CreateServices(db.Database.GetConnectionString()!, control, secondHandler: true);
        var sut = ActivatorUtilities.CreateInstance<OutboxWorker<IdentityOutboxStore>>(services);

        // Act
        await sut.StartAsync(TestContext.Current.CancellationToken);
        await control.SecondStarted.Task.WaitAsync(TimeSpan.FromSeconds(15), TestContext.Current.CancellationToken);
        var during = await db.OutboxMessages.AsNoTracking().SingleAsync(item => item.Id == message.Id, TestContext.Current.CancellationToken);
        control.SecondRelease.TrySetResult();
        var actual = await WaitForStatusAsync(message.Id, OutboxStatus.Completed);
        await sut.StopAsync(TestContext.Current.CancellationToken);
        sut.Dispose();

        // Assert
        during.Status.Should().Be(OutboxStatus.Processing);
        actual.Status.Should().Be(OutboxStatus.Completed);
        control.Calls.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCompletedEventIsReplayed_ShouldReuseEventIdAndRecordReplay()
    {
        // Arrange
        using var setup = factory.Services.CreateScope();
        var db = setup.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.OutboxMessages.ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        var message = Message(new TestEvent(Slow: false));
        db.OutboxMessages.Add(message);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var control = new WorkerControl();
        await using var services = CreateServices(db.Database.GetConnectionString()!, control);
        var sut = ActivatorUtilities.CreateInstance<OutboxWorker<IdentityOutboxStore>>(services);
        await sut.StartAsync(TestContext.Current.CancellationToken);
        await WaitForStatusAsync(message.Id, OutboxStatus.Completed);

        // Act
        using (var scope = services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IdentityOutboxStore>()
                .ReplayAsync(message.Id, "test-operator", TestContext.Current.CancellationToken);
        }
        using (var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
        {
            while (Volatile.Read(ref control.Calls) < 2) await Task.Delay(50, deadline.Token);
        }
        var actual = await WaitForStatusAsync(message.Id, OutboxStatus.Completed);
        await sut.StopAsync(TestContext.Current.CancellationToken);
        sut.Dispose();

        // Assert
        actual.Id.Should().Be(message.Id);
        actual.ProcessingCycle.Should().Be(1);
        actual.ReplayCount.Should().Be(1);
        actual.LastReplayedBy.Should().Be("test-operator");
        actual.CumulativeAttemptCount.Should().Be(2);
        control.EventIds.Should().HaveCount(2).And.OnlyContain(id => id == message.Id);
        control.Logs.Should().Contain(entry => entry.Contains("Outbox replay", StringComparison.Ordinal)
            && entry.Contains(message.Id.ToString()) && entry.Contains(message.CorrelationId));
        control.Logger.Properties.Should().Contain(entry =>
            entry.Any(pair => pair.Key == "EventId" && Equals(pair.Value, message.Id)) &&
            entry.Any(pair => pair.Key == "CorrelationId" && Equals(pair.Value, message.CorrelationId)) &&
            entry.Any(pair => pair.Key == "Actor" && Equals(pair.Value, "test-operator")));
    }

    [Fact]
    public async Task ExecuteAsync_WhenBacklogExceedsCapacity_ShouldRespectConcurrencyAndClaimLimit()
    {
        // Arrange
        using var setup = factory.Services.CreateScope();
        var db = setup.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.OutboxMessages.ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        var messages = Enumerable.Range(0, 5).Select(_ => Message()).ToArray();
        db.OutboxMessages.AddRange(messages);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var control = new WorkerControl();
        await using var services = CreateServices(db.Database.GetConnectionString()!, control, batchSize: 1, concurrency: 2);
        var sut = ActivatorUtilities.CreateInstance<OutboxWorker<IdentityOutboxStore>>(services);

        // Act
        await sut.StartAsync(TestContext.Current.CancellationToken);
        using (var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
        {
            while (Volatile.Read(ref control.Calls) < 2) await Task.Delay(50, deadline.Token);
        }
        var processing = await db.OutboxMessages.AsNoTracking().CountAsync(item => item.Status == OutboxStatus.Processing, TestContext.Current.CancellationToken);
        var pending = await db.OutboxMessages.AsNoTracking().CountAsync(item => item.Status == OutboxStatus.Pending, TestContext.Current.CancellationToken);
        control.Release.TrySetResult();
        foreach (var message in messages) await WaitForStatusAsync(message.Id, OutboxStatus.Completed);
        await sut.StopAsync(TestContext.Current.CancellationToken);
        sut.Dispose();

        // Assert
        processing.Should().Be(2);
        pending.Should().Be(3);
        control.MaxConcurrent.Should().Be(2);
        control.ScopeIds.Should().OnlyHaveUniqueItems().And.HaveCount(5);
    }

    internal static ServiceProvider CreateServices(string connectionString, WorkerControl control, bool registerHandler = true,
        bool preflight = false, bool throwAfterHandler = false, bool retry = false, bool shortTimeout = false, bool expiry = false,
        bool shortShutdown = false,
        IEmailDispatchAvailability? smtpAvailability = null, IEmailSender? sender = null, int batchSize = 20, int concurrency = 4,
        TimeSpan? fallbackDelay = null, bool secondHandler = false, bool brokenDependency = false)
    {
        var services = new ServiceCollection();
        services.AddLogging(logging => logging.AddProvider(control.Logger));
        services.AddDbContext<IdentityDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<IIdentityDbContext>(provider => provider.GetRequiredService<IdentityDbContext>());
        services.AddOutbox<IdentityOutboxStore>(new ConfigurationBuilder().Build());
        services.AddScoped<FaultingStore>();
        services.Configure<OutboxOptions>(options =>
        {
            options.PollInterval = TimeSpan.FromMilliseconds(50);
            options.BatchSize = batchSize;
            options.Concurrency = concurrency;
            options.MaxAttempts = 1;
            options.LeaseDuration = TimeSpan.FromSeconds(10);
            options.RenewalInterval = TimeSpan.FromSeconds(1);
            options.HandlerTimeout = TimeSpan.FromSeconds(5);
            options.CleanupInterval = TimeSpan.FromMilliseconds(50);
            options.CleanupBatchSize = 1;
            if (fallbackDelay is not null) options.PreflightFallbackDelay = fallbackDelay.Value;
            if (retry)
            {
                options.MaxAttempts = 2;
                options.RetryInitialDelay = TimeSpan.FromMilliseconds(100);
                options.RetryMaximumDelay = TimeSpan.FromMilliseconds(100);
            }
            if (shortTimeout) options.HandlerTimeout = TimeSpan.FromMilliseconds(500);
            if (shortShutdown) options.ShutdownTimeout = TimeSpan.FromMilliseconds(100);
        });
        services.AddSingleton(control);
        services.AddRequests(Array.Empty<Assembly>());
        if (registerHandler) services.AddScoped<IEventHandler<TestEvent>, TestHandler>();
        if (brokenDependency) services.AddScoped<IEventHandler<TestEvent>>(_ => throw new InvalidOperationException("sensitive-test-token"));
        if (secondHandler) services.AddScoped<IEventHandler<TestEvent>, SecondHandler>();
        if (sender is not null)
        {
            services.AddSingleton(sender);
            services.AddScoped<IEventHandler<TestEvent>, EmailAcceptedHandler>();
        }
        if (throwAfterHandler) services.AddScoped<IEventHandler<TestEvent>, CircuitOpenHandler>();
        if (preflight) services.AddScoped<IOutboxPreflight<TestEvent>, TestPreflight>();
        if (expiry) services.AddScoped<IOutboxExpiry<TestEvent>, TestExpiry>();
        if (smtpAvailability is not null)
        {
            services.AddSingleton(smtpAvailability);
            services.AddScoped<IOutboxPreflight<TestEvent>, SmtpOutboxPreflight<TestEvent>>();
            services.AddScoped<IEventHandler<IndependentEvent>, IndependentHandler>();
        }
        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<OutboxRegistry<IdentityOutboxStore>>()
            .Register<TestEvent>("test.event.v1")
            .Register<IndependentEvent>("independent.event.v1");

        return provider;
    }

    private async Task<OutboxMessage> WaitForStatusAsync(Guid id, OutboxStatus status)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        while (true)
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var message = await db.OutboxMessages.AsNoTracking().SingleAsync(item => item.Id == id, deadline.Token);
            if (message.Status == status)
            {
                return message;
            }

            await Task.Delay(50, deadline.Token);
        }
    }

    private static OutboxMessage Message(TestEvent? value = null, string eventType = "test.event.v1", string? payload = null) => new()
    {
        Id = Guid.NewGuid(),
        EventType = eventType,
        Payload = payload ?? JsonSerializer.Serialize(value ?? new TestEvent()),
        CorrelationId = Guid.NewGuid().ToString(),
        CreatedAt = DateTimeOffset.UtcNow
    };

    public sealed record TestEvent(bool Slow = true, bool Defer = false, bool Expired = false) : IEvent;
    public sealed record IndependentEvent : IEvent;
    public sealed class WorkerControl
    {
        public int Calls;
        public int Active;
        public int MaxConcurrent;
        public int CleanupCalls;
        public bool IgnoreCancellation { get; init; }
        public RecordingProvider Logger { get; } = new();
        public ConcurrentQueue<string> Logs => Logger.Messages;
        public ConcurrentBag<Guid> ScopeIds { get; } = [];
        public ConcurrentBag<Guid> EventIds { get; } = [];
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource SecondStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource SecondRelease { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Finished { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public sealed class TestHandler(WorkerControl control, OutboxDispatchContext dispatchContext) : IEventHandler<TestEvent>
    {
        private readonly Guid _scopeId = Guid.NewGuid();

        public async Task HandleAsync(TestEvent @event, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref control.Calls);
            var active = Interlocked.Increment(ref control.Active);
            int previous;
            do
            {
                previous = Volatile.Read(ref control.MaxConcurrent);
                if (active <= previous) break;
            } while (Interlocked.CompareExchange(ref control.MaxConcurrent, active, previous) != previous);
            control.ScopeIds.Add(_scopeId);
            control.EventIds.Add(dispatchContext.EventId);
            control.Started.TrySetResult();
            try
            {
                if (@event.Slow)
                {
                    await (control.IgnoreCancellation
                        ? control.Release.Task
                        : control.Release.Task.WaitAsync(cancellationToken));
                }
            }
            finally
            {
                Interlocked.Decrement(ref control.Active);
                control.Finished.TrySetResult();
            }
        }
    }

    public sealed class TestPreflight : IOutboxPreflight<TestEvent>
    {
        public ValueTask<OutboxAvailability> CheckAsync(TestEvent value, CancellationToken cancellationToken) =>
            ValueTask.FromResult(value.Defer
                ? new OutboxAvailability(false, DateTimeOffset.UtcNow.AddSeconds(10))
                : new OutboxAvailability(true));
    }

    public sealed class CircuitOpenHandler : IEventHandler<TestEvent>
    {
        public Task HandleAsync(TestEvent @event, CancellationToken cancellationToken) =>
            throw new EmailCircuitOpenException(null);
    }

    public sealed class EmailAcceptedHandler(IEmailSender sender) : IEventHandler<TestEvent>
    {
        public Task HandleAsync(TestEvent @event, CancellationToken cancellationToken) =>
            sender.SendAsync(new EmailMessage("from@example.test", "to@example.test", "subject", "body"), cancellationToken);
    }

    public sealed class SecondHandler(WorkerControl control) : IEventHandler<TestEvent>
    {
        public async Task HandleAsync(TestEvent @event, CancellationToken cancellationToken)
        {
            control.SecondStarted.TrySetResult();
            await control.SecondRelease.Task.WaitAsync(cancellationToken);
        }
    }

    public sealed class IndependentHandler(WorkerControl control) : IEventHandler<IndependentEvent>
    {
        public Task HandleAsync(IndependentEvent @event, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref control.Calls);

            return Task.CompletedTask;
        }
    }

    public sealed class TestExpiry : IOutboxExpiry<TestEvent>
    {
        public bool IsExpired(TestEvent value, DateTimeOffset now) => value.Expired;
    }

    public sealed class RecordingProvider : ILoggerProvider
    {
        public ConcurrentQueue<string> Messages { get; } = new();
        public ConcurrentQueue<IReadOnlyList<KeyValuePair<string, object?>>> Properties { get; } = new();
        public ILogger CreateLogger(string categoryName) => new RecordingLogger(Messages, Properties);
        public void Dispose() { }

        private sealed class RecordingLogger(ConcurrentQueue<string> messages,
            ConcurrentQueue<IReadOnlyList<KeyValuePair<string, object?>>> properties) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                messages.Enqueue(formatter(state, exception));
                if (state is IReadOnlyList<KeyValuePair<string, object?>> values) properties.Enqueue(values.ToArray());
            }

        }
    }

    internal sealed class FaultingStore(IdentityOutboxStore inner, WorkerControl control) : IOutboxStore
    {
        public Task<int> CleanupAsync(DateTimeOffset cutoff, int batchSize, CancellationToken cancellationToken) =>
            Interlocked.Increment(ref control.CleanupCalls) == 1
                ? Task.FromException<int>(new InvalidOperationException())
                : inner.CleanupAsync(cutoff, batchSize, cancellationToken);
        public Task<IReadOnlyList<OutboxClaim>> ClaimAsync(int count, OutboxOptions options, CancellationToken cancellationToken) => inner.ClaimAsync(count, options, cancellationToken);
        public Task<bool> RenewAsync(OutboxClaim claim, OutboxOptions options, CancellationToken cancellationToken) => inner.RenewAsync(claim, options, cancellationToken);
        public Task<bool> CompleteAsync(OutboxClaim claim, CancellationToken cancellationToken) => inner.CompleteAsync(claim, cancellationToken);
        public Task<bool> FailAsync(OutboxClaim claim, string error, TimeSpan delay, int maxAttempts, CancellationToken cancellationToken) => inner.FailAsync(claim, error, delay, maxAttempts, cancellationToken);
        public Task<bool> DeferAsync(OutboxClaim claim, DateTimeOffset nextAttempt, CancellationToken cancellationToken) => inner.DeferAsync(claim, nextAttempt, cancellationToken);
        public Task<bool> ExpireAsync(OutboxClaim claim, CancellationToken cancellationToken) => inner.ExpireAsync(claim, cancellationToken);
        public Task<OutboxDiagnostics> GetDiagnosticsAsync(CancellationToken cancellationToken) => inner.GetDiagnosticsAsync(cancellationToken);
    }
}
