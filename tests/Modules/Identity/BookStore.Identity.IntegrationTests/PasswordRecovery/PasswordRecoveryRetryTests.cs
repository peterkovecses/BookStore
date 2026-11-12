namespace BookStore.Identity.IntegrationTests.PasswordRecovery;

public sealed class PasswordRecoveryRetryTests(IdentityFactory factory) : IClassFixture<IdentityFactory>
{
    [Fact]
    public async Task ExecuteAsync_WhenIntermediateRequestIsExpiredOrLegacyAndStale_ShouldNotCreateEmailEvent()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var email = $"{Guid.NewGuid():N}@example.com";
        var sender = Substitute.For<IEmailSender>();
        var now = DateTimeOffset.UtcNow;
        var dataProtection = factory.Services.GetRequiredService<IDataProtectionProvider>();
        var protectedEmail = dataProtection.CreateProtector(PasswordRecoveryRequested.ProtectionPurpose).Protect(email);
        Guid expiredEventId;
        Guid legacyEventId;
        var expiredCorrelationId = Guid.NewGuid().ToString();
        var legacyCorrelationId = Guid.NewGuid().ToString();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid().ToString(), Email = email, UserName = email, FirstName = "Jane", LastName = "Doe"
            };
            (await users.CreateAsync(user, "Password1!")).Succeeded.Should().BeTrue();
            expiredEventId = Guid.NewGuid();
            legacyEventId = Guid.NewGuid();
            db.OutboxMessages.AddRange(
                new OutboxMessage
                {
                    Id = expiredEventId,
                    EventType = PasswordRecoveryRequested.EventType,
                    Payload = JsonSerializer.Serialize(new PasswordRecoveryRequested(protectedEmail, now.AddMinutes(-1))),
                    CorrelationId = expiredCorrelationId,
                    CreatedAt = now
                },
                new OutboxMessage
                {
                    Id = legacyEventId,
                    EventType = PasswordRecoveryRequested.EventType,
                    Payload = JsonSerializer.Serialize(new { ProtectedEmail = protectedEmail }),
                    CorrelationId = legacyCorrelationId,
                    CreatedAt = now.AddHours(-3)
                });
            await db.SaveChangesAsync(cancellationToken);
        }

        await using var host = CreateHost(sender);
        using var recoveryScope = host.Services.CreateScope();
        var recoveryDb = recoveryScope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        using var worker = ActivatorUtilities.CreateInstance<OutboxWorker<IdentityOutboxStore>>(host.Services);

        try
        {
            // Act
            await worker.StartAsync(cancellationToken);
            var expired = await WaitAsync(recoveryDb, item => item.Id == expiredEventId && item.Status == OutboxStatus.Expired, cancellationToken);
            var legacy = await WaitAsync(recoveryDb, item => item.Id == legacyEventId && item.Status == OutboxStatus.Expired, cancellationToken);
            var emailEventCount = await recoveryDb.OutboxMessages.AsNoTracking()
                .CountAsync(item => item.EventType == PasswordRecoveryRequestedHandler.EmailEventType &&
                    (item.CorrelationId == expiredCorrelationId || item.CorrelationId == legacyCorrelationId), cancellationToken);

            // Assert
            expired.Status.Should().Be(OutboxStatus.Expired);
            legacy.Status.Should().Be(OutboxStatus.Expired);
            emailEventCount.Should().Be(0);
            await sender.DidNotReceive().SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>());
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenParentIsReplayedAfterChildWasSaved_ShouldNotMintAnotherTokenOrEmail()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var email = $"{Guid.NewGuid():N}@example.com";
        var now = DateTimeOffset.UtcNow;
        var parentId = Guid.NewGuid();
        var correlationId = Guid.NewGuid().ToString();
        var sender = Substitute.For<IEmailSender>();
        var senderCalls = 0;
        sender.SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            Interlocked.Increment(ref senderCalls);

            return Task.CompletedTask;
        });
        await using var host = CreateHost(sender, maxAttempts: 1);
        var dataProtection = host.Services.GetRequiredService<IDataProtectionProvider>();
        var value = new PasswordRecoveryRequested(
            dataProtection.CreateProtector(PasswordRecoveryRequested.ProtectionPurpose).Protect(email),
            now.AddHours(1));
        using (var scope = factory.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            var db = services.GetRequiredService<IdentityDbContext>();
            var users = services.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid().ToString(), Email = email, UserName = email, FirstName = "Jane", LastName = "Doe"
            };
            (await users.CreateAsync(user, "Password1!")).Succeeded.Should().BeTrue();
            var parent = new OutboxMessage
            {
                Id = parentId,
                EventType = PasswordRecoveryRequested.EventType,
                Payload = JsonSerializer.Serialize(value),
                CorrelationId = correlationId,
                CreatedAt = now
            };
            db.OutboxMessages.Add(parent);
            await db.SaveChangesAsync(cancellationToken);
        }

        using var recoveryScope = host.Services.CreateScope();
        var recoveryDb = recoveryScope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var outbox = host.Services.GetRequiredService<IIdentityOutboxOperations>();
        var firstWorker = ActivatorUtilities.CreateInstance<OutboxWorker<IdentityOutboxStore>>(host.Services);
        var secondWorker = ActivatorUtilities.CreateInstance<OutboxWorker<IdentityOutboxStore>>(host.Services);

        try
        {
            // Act
            await firstWorker.StartAsync(cancellationToken);
            var processedParent = await WaitAsync(recoveryDb, item => item.Id == parentId &&
                (item.Status == OutboxStatus.Completed || item.Status == OutboxStatus.Failed), cancellationToken);
            processedParent.Status.Should().Be(OutboxStatus.Completed, $"the stored error type was {processedParent.LastError}");
            var receipt = await recoveryDb.PasswordRecoveryRequestReceipts.AsNoTracking()
                .SingleAsync(item => item.RequestEventId == parentId, cancellationToken);
            var initialChild = await recoveryDb.OutboxMessages.AsNoTracking()
                .SingleAsync(item => item.Id == receipt.EmailEventId, cancellationToken);
            await WaitAsync(recoveryDb, item => item.Id == initialChild.Id && item.Status == OutboxStatus.Completed, cancellationToken);
            await firstWorker.StopAsync(CancellationToken.None);
            (await outbox.ReplayAsync(parentId, "integration-test", cancellationToken)).Should().BeTrue();
            await secondWorker.StartAsync(cancellationToken);
            var parent = await WaitAsync(recoveryDb, item => item.Id == parentId && item.Status == OutboxStatus.Completed, cancellationToken);
            var emailEvent = await WaitAsync(recoveryDb, item =>
                item.Id == initialChild.Id && item.Status == OutboxStatus.Completed, cancellationToken);
            var childCount = await recoveryDb.OutboxMessages.AsNoTracking()
                .CountAsync(item => item.EventType == PasswordRecoveryRequestedHandler.EmailEventType &&
                    item.CorrelationId == correlationId, cancellationToken);

            // Assert
            parent.Status.Should().Be(OutboxStatus.Completed);
            receipt.EmailEventId.Should().Be(emailEvent.Id);
            emailEvent.Id.Should().Be(initialChild.Id);
            emailEvent.CorrelationId.Should().Be(correlationId);
            childCount.Should().Be(1);
            senderCalls.Should().Be(1);
            factory.Logs.Messages.Should().NotContain(log => log.Contains(email) || log.Contains(value.ProtectedEmail));
        }
        finally
        {
            await firstWorker.StopAsync(CancellationToken.None);
            await secondWorker.StopAsync(CancellationToken.None);
            firstWorker.Dispose();
            secondWorker.Dispose();
        }
    }

    [Fact]
    public async Task ExecuteAsync_WhenDeliveryFailsAndHostRestarts_ShouldRetryProtectedPayloadAndValidateToken()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var email = $"{Guid.NewGuid():N}@example.com";
        var delivered = new TaskCompletionSource<EmailMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var count = 0;
        var sender = Substitute.For<IEmailSender>();
        sender.SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            if (Interlocked.Increment(ref count) == 1)
            {
                return Task.FromException(new InvalidOperationException("Simulated delivery failure"));
            }

            delivered.TrySetResult(call.Arg<EmailMessage>());

            return Task.CompletedTask;
        });
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid().ToString(), Email = email, UserName = email, FirstName = "Jane", LastName = "Doe"
            };
            (await users.CreateAsync(user, "Password1!")).Succeeded.Should().BeTrue();
        }

        Guid eventId;
        string payload;
        await using (var producer = CreateHost(sender))
        {
            using var client = producer.CreateClient();
            using var queued = await client.PostAsJsonAsync("/api/identity/password/forgot", new { Email = email }, cancellationToken);
            queued.StatusCode.Should().Be(HttpStatusCode.NoContent);
            using var scope = producer.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            using var worker = ActivatorUtilities.CreateInstance<OutboxWorker<IdentityOutboxStore>>(producer.Services);
            try
            {
                await worker.StartAsync(cancellationToken);
                var failed = await WaitAsync(db, item => item.EventType == PasswordRecoveryRequestedHandler.EmailEventType &&
                    item.Status == OutboxStatus.Pending && item.AttemptCount == 1, cancellationToken);
                eventId = failed.Id;
                payload = failed.Payload;
            }
            finally
            {
                await worker.StopAsync(CancellationToken.None);
            }
        }

        // Act
        await using var recovery = CreateHost(sender);
        using var recoveryScope = recovery.Services.CreateScope();
        var recoveryDb = recoveryScope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        using var recoveryWorker = ActivatorUtilities.CreateInstance<OutboxWorker<IdentityOutboxStore>>(recovery.Services);
        try
        {
            await recoveryWorker.StartAsync(cancellationToken);
            var completed = await WaitAsync(recoveryDb, item => item.Id == eventId && item.Status == OutboxStatus.Completed, cancellationToken);
            var message = await delivered.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            var link = Regex.Match(message.TextBody, @"https://[^\s]+").Value;
            var query = QueryHelpers.ParseQuery(new Uri(link).Query);
            using var client = recovery.CreateClient();
            using var reset = await client.PostAsJsonAsync("/api/identity/password/reset", new
            {
                UserId = query["userId"].ToString(), Token = query["token"].ToString(), NewPassword = "Different2!"
            }, cancellationToken);

            // Assert
            completed.AttemptCount.Should().Be(2);
            completed.Payload.Should().Be(payload);
            count.Should().Be(2);
            payload.Should().NotContain(link).And.NotContain(query["token"].ToString());
            string.Join('\n', factory.Logs.Messages).Should().NotContain(link).And.NotContain(query["token"].ToString()).And.NotContain("Different2!");
            reset.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }
        finally
        {
            await recoveryWorker.StopAsync(CancellationToken.None);
        }
    }

    private WebApplicationFactory<Program> CreateHost(IEmailSender sender, int maxAttempts = 8) =>
        factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Outbox:PollInterval", "00:00:00.100");
            builder.UseSetting("Outbox:RetryInitialDelay", "00:00:05");
            builder.UseSetting("Outbox:MaxAttempts", maxAttempts.ToString());
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IEmailSender>();
                services.AddSingleton(sender);
            });
        });

    private static async Task<OutboxMessage> WaitAsync(
        IdentityDbContext db, Func<OutboxMessage, bool> predicate, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(45));
        while (true)
        {
            var messages = await db.OutboxMessages.AsNoTracking().ToListAsync(deadline.Token);
            var match = messages.FirstOrDefault(predicate);
            if (match is not null) return match;
            await Task.Delay(100, deadline.Token);
        }
    }
}
