namespace BookStore.Identity.IntegrationTests.PasswordRecovery;

public sealed class PasswordRecoveryOutboxTests(IdentityOutboxTestApplicationFactory factory)
    : IClassFixture<IdentityOutboxTestApplicationFactory>
{
    [Fact]
    public async Task ExecuteAsync_WhenCircuitDefersPastExpiry_ShouldExpireAndSendOnlyFreshRequest()
    {
        // Arrange
        const string forgotPath = "/api/identity/password/forgot";
        const string resetPath = "/api/identity/password/reset";
        var cancellationToken = TestContext.Current.CancellationToken;
        long cooldownOffsetTicks = 0;
        var cooldownClock = Substitute.For<TimeProvider>();
        cooldownClock.GetUtcNow().Returns(_ =>
            TimeProvider.System.GetUtcNow().AddTicks(Interlocked.Read(ref cooldownOffsetTicks)));
        await using var host = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Identity:PasswordRecovery:TokenLifetime", "00:00:10");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IEmailRequestCooldownStore>();
                services.AddScoped<IEmailRequestCooldownStore>(provider => new EmailRequestCooldownStore(
                    provider.GetRequiredService<IIdentityDbContext>(),
                    provider.GetRequiredService<ILookupNormalizer>(), cooldownClock));
            });
        });
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var email = $"{Guid.NewGuid():N}@example.com";
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(), Email = email, UserName = email, FirstName = "Jane", LastName = "Doe"
        };
        (await users.CreateAsync(user, "Password1!")).Succeeded.Should().BeTrue();
        db.RefreshTokens.Add(new RefreshToken
        {
            Hash = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)),
            FamilyId = Guid.NewGuid(), UserId = user.Id, ExpiresAt = DateTimeOffset.UtcNow.AddDays(1)
        });
        await db.SaveChangesAsync(cancellationToken);
        using var client = host.CreateClient();
        using var mailpit = new HttpClient { BaseAddress = factory.MailpitAddress };
        using var queued = await client.PostAsJsonAsync(forgotPath, new { Email = email }, cancellationToken);
        queued.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var cooldown = queued.Headers.RetryAfter!.Delta!.Value;
        var circuit = host.Services.GetRequiredService<IEmailDispatchAvailability>();
        circuit.GetType().GetMethod("Complete")!.Invoke(circuit, [false, true, false]);
        var worker = ActivatorUtilities.CreateInstance<OutboxWorker<IdentityOutboxStore>>(host.Services);

        try
        {
            // Act
            await worker.StartAsync(cancellationToken);
            var stale = await WaitAsync(db, item => item.EventType == PasswordRecoveryRequestedHandler.EmailEventType &&
                item.Status == OutboxStatus.Pending && item.NextAttemptAt is not null, cancellationToken);
            using var unknown = await client.PostAsJsonAsync(forgotPath, new { Email = $"{Guid.NewGuid():N}@example.com" }, cancellationToken);
            var unrelated = await WaitAsync(db, item => item.EventType == PasswordRecoveryRequested.EventType &&
                item.Status == OutboxStatus.Completed && item.CreatedAt > stale.CreatedAt, cancellationToken);
            await worker.StopAsync(cancellationToken);
            worker.Dispose();
            var value = JsonSerializer.Deserialize<PasswordResetEmailRequested>(stale.Payload)!;
            var staleLink = host.Services.GetRequiredService<IDataProtectionProvider>()
                .CreateProtector("BookStore.Identity.PasswordResetLink.v2")
                .CreateProtector(value.UserId).Unprotect(value.ProtectedLink);
            var staleQuery = QueryHelpers.ParseQuery(new Uri(staleLink).Query);
            var remaining = value.ExpiresAt.AddSeconds(1) - DateTimeOffset.UtcNow;
            if (remaining > TimeSpan.Zero) await Task.Delay(remaining, cancellationToken);
            using var expiredReset = await client.PostAsJsonAsync(resetPath, new
            {
                UserId = staleQuery["userId"].ToString(), Token = staleQuery["token"].ToString(), NewPassword = "Different2!"
            }, cancellationToken);
            var afterExpired = await db.RefreshTokens.AsNoTracking().SingleAsync(item => item.UserId == user.Id, cancellationToken);
            var passwordAfterExpired = await users.CheckPasswordAsync(user, "Password1!");
            worker = ActivatorUtilities.CreateInstance<OutboxWorker<IdentityOutboxStore>>(host.Services);
            await worker.StartAsync(cancellationToken);
            await WaitAsync(db, item => item.Id == stale.Id && item.Status == OutboxStatus.Expired, cancellationToken);
            using var empty = await mailpit.GetFromJsonAsync<JsonDocument>("/api/v1/messages", cancellationToken);
            Interlocked.Exchange(ref cooldownOffsetTicks, cooldown.Ticks);
            using var freshRequest = await client.PostAsJsonAsync(forgotPath, new { Email = email }, cancellationToken);
            freshRequest.StatusCode.Should().Be(HttpStatusCode.NoContent);
            var fresh = await WaitAsync(db, item => item.EventType == PasswordRecoveryRequestedHandler.EmailEventType &&
                item.Id != stale.Id && item.Status == OutboxStatus.Completed, cancellationToken);
            using var messages = await mailpit.GetFromJsonAsync<JsonDocument>("/api/v1/messages", cancellationToken);
            var id = messages!.RootElement.GetProperty("messages")[0].GetProperty("ID").GetString();
            using var message = await mailpit.GetFromJsonAsync<JsonDocument>($"/api/v1/message/{id}", cancellationToken);
            var text = message!.RootElement.GetProperty("Text").GetString()!;
            var link = Regex.Match(text, @"https://[^\s]+").Value;
            var query = QueryHelpers.ParseQuery(new Uri(link).Query);
            using var reset = await client.PostAsJsonAsync(resetPath, new
            {
                UserId = query["userId"].ToString(), Token = query["token"].ToString(), NewPassword = "Different2!"
            }, cancellationToken);

            // Assert
            unknown.StatusCode.Should().Be(HttpStatusCode.NoContent);
            unrelated.Status.Should().Be(OutboxStatus.Completed);
            stale.AttemptCount.Should().Be(0);
            empty!.RootElement.GetProperty("total").GetInt32().Should().Be(0);
            expiredReset.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            afterExpired.Revoked.Should().BeFalse();
            passwordAfterExpired.Should().BeTrue();
            messages.RootElement.GetProperty("total").GetInt32().Should().Be(1);
            fresh.Payload.Should().NotContain(link).And.NotContain(query["token"].ToString());
            message.RootElement.GetProperty("HTML").GetString().Should().Contain("reset-password");
            reset.StatusCode.Should().Be(HttpStatusCode.NoContent);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
            worker.Dispose();
        }
    }

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
