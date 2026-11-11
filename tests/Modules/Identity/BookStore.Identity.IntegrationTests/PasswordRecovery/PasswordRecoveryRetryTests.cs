namespace BookStore.Identity.IntegrationTests.PasswordRecovery;

public sealed class PasswordRecoveryRetryTests(IdentityFactory factory) : IClassFixture<IdentityFactory>
{
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

    private WebApplicationFactory<Program> CreateHost(IEmailSender sender) =>
        factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Outbox:PollInterval", "00:00:00.100");
            builder.UseSetting("Outbox:RetryInitialDelay", "00:00:05");
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
