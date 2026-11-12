namespace BookStore.Identity.IntegrationTests;

[Collection("Outbox integration")]
public sealed class VerificationOutboxIntegrationTests(IdentityOutboxTestApplicationFactory factory)
    : IClassFixture<IdentityOutboxTestApplicationFactory>
{
    [Fact]
    public async Task ExecuteAsync_WhenVerificationExpiresAndIsResent_ShouldDeliverFreshLinkAndConfirmAccount()
    {
        // Arrange
        const string registrationPath = "/api/identity/registration";
        const string resendPath = "/api/identity/verification/resend";
        const string confirmationPath = "/api/identity/verification/confirmation";
        var cancellationToken = TestContext.Current.CancellationToken;
        var email = $"{Guid.NewGuid():N}@example.com";
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        using var mailpit = new HttpClient { BaseAddress = factory.MailpitAddress };
        using var registration = await client.PostAsJsonAsync(registrationPath, new RegistrationRequest
        {
            Email = email,
            Password = "Password1!",
            FirstName = "Jane",
            LastName = "Doe"
        }, cancellationToken);
        registration.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var user = await db.Users.SingleAsync(item => item.Email == email, cancellationToken);
        var original = await db.OutboxMessages.SingleAsync(cancellationToken);
        var circuit = factory.Services.GetRequiredService<IEmailDispatchAvailability>();
        circuit.GetType().GetMethod("Complete")!.Invoke(circuit, [false, true, false]);
        circuit.GetAvailability().IsAvailable.Should().BeFalse();
        var worker = ActivatorUtilities.CreateInstance<OutboxWorker<IdentityOutboxStore>>(factory.Services);

        try
        {
            // Act
            await worker.StartAsync(cancellationToken);
            var deferred = await WaitForDeferralAsync(db, original.Id, cancellationToken);
            deferred.AttemptCount.Should().Be(0);
            deferred.CumulativeAttemptCount.Should().Be(0);
            using var deferredMessages = await mailpit.GetFromJsonAsync<JsonDocument>("/api/v1/messages", cancellationToken);
            deferredMessages!.RootElement.GetProperty("total").GetInt32().Should().Be(0);
            await worker.StopAsync(cancellationToken);
            worker.Dispose();
            var originalEvent = JsonSerializer.Deserialize<VerificationEmailRequested>(original.Payload)!;
            var remaining = originalEvent.ExpiresAt.AddSeconds(1) - DateTimeOffset.UtcNow;
            if (remaining > TimeSpan.Zero)
            {
                await Task.Delay(remaining, cancellationToken);
            }
            worker = ActivatorUtilities.CreateInstance<OutboxWorker<IdentityOutboxStore>>(factory.Services);
            await worker.StartAsync(cancellationToken);
            await WaitForStatusAsync(db, original.Id, OutboxStatus.Expired, cancellationToken);
            using var emptyMessages = await mailpit.GetFromJsonAsync<JsonDocument>("/api/v1/messages", cancellationToken);
            emptyMessages!.RootElement.GetProperty("total").GetInt32().Should().Be(0);
            using var resend = await client.PostAsJsonAsync(resendPath, new { Email = email }, cancellationToken);
            resend.StatusCode.Should().Be(HttpStatusCode.NoContent);
            OutboxMessage? fresh;
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                deadline.CancelAfter(TimeSpan.FromSeconds(30));
                do
                {
                    fresh = await db.OutboxMessages.AsNoTracking().SingleOrDefaultAsync(
                        item => item.Id != original.Id && item.EventType == VerificationRequestFactory.EventType, deadline.Token);
                    if (fresh is null) await Task.Delay(100, deadline.Token);
                } while (fresh is null);
            }
            await WaitForStatusAsync(db, fresh.Id, OutboxStatus.Completed, cancellationToken);
            using var messages = await mailpit.GetFromJsonAsync<JsonDocument>("/api/v1/messages", cancellationToken);
            var id = messages!.RootElement.GetProperty("messages")[0].GetProperty("ID").GetString();
            using var message = await mailpit.GetFromJsonAsync<JsonDocument>($"/api/v1/message/{id}", cancellationToken);
            var text = message!.RootElement.GetProperty("Text").GetString()!;
            var link = Regex.Match(text, @"https://[^\s]+").Value;
            var query = QueryHelpers.ParseQuery(new Uri(link).Query);
            using var confirmation = await client.PostAsJsonAsync(confirmationPath, new
            {
                UserId = query["userId"].ToString(),
                Token = query["token"].ToString()
            }, cancellationToken);
            db.ChangeTracker.Clear();
            var confirmed = await db.Users.SingleAsync(item => item.Id == user.Id, cancellationToken);

            // Assert
            messages.RootElement.GetProperty("total").GetInt32().Should().Be(1);
            new Uri(link).GetLeftPart(UriPartial.Path).Should().Be("https://localhost/verify-email");
            query["userId"].ToString().Should().Be(user.Id);
            query["token"].ToString().Should().NotBeNullOrEmpty();
            fresh.Id.Should().NotBe(original.Id);
            confirmation.StatusCode.Should().Be(HttpStatusCode.NoContent);
            confirmed.EmailConfirmed.Should().BeTrue();
        }

        finally
        {
            await worker.StopAsync(CancellationToken.None);
            worker.Dispose();
        }
    }

    private static async Task<OutboxMessage> WaitForDeferralAsync(IdentityDbContext db, Guid id, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        while (true)
        {
            var value = await db.OutboxMessages.AsNoTracking().SingleAsync(item => item.Id == id, deadline.Token);
            if (value.Status == OutboxStatus.Pending && value.NextAttemptAt is not null)
            {
                return value;
            }

            await Task.Delay(50, deadline.Token);
        }
    }

    private static async Task WaitForStatusAsync(IdentityDbContext db, Guid id, OutboxStatus status, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        while (!await db.OutboxMessages.AsNoTracking().AnyAsync(item => item.Id == id && item.Status == status, deadline.Token))
        {
            await Task.Delay(100, deadline.Token);
        }
    }
}
