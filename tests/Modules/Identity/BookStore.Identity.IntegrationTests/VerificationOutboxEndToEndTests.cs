namespace BookStore.Identity.IntegrationTests;

public sealed class VerificationOutboxEndToEndFactory : BaseWebApplicationFactory<Program>
{
    private const int SmtpPort = 1025;
    private const int HttpPort = 8025;
    private readonly MsSqlContainer _database = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
    private readonly IContainer _mailpit = new ContainerBuilder("axllent/mailpit:latest")
        .WithPortBinding(SmtpPort, true)
        .WithPortBinding(HttpPort, true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilInternalTcpPortIsAvailable(SmtpPort))
        .Build();

    public Uri MailpitAddress => new($"http://{_mailpit.Hostname}:{_mailpit.GetMappedPublicPort(HttpPort)}");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("ConnectionStrings:BookStoreDb", _database.GetConnectionString());
        builder.UseSetting("ConnectionStrings:book-covers", "UseDevelopmentStorage=true");
        builder.UseSetting("EmailDispatcher:Smtp:Host", _mailpit.Hostname);
        builder.UseSetting("EmailDispatcher:Smtp:Port", _mailpit.GetMappedPublicPort(SmtpPort).ToString());
        builder.UseSetting("EmailDispatcher:Smtp:Security", "None");
        builder.UseSetting("EmailDispatcher:Smtp:Username", "");
        builder.UseSetting("EmailDispatcher:Smtp:Password", "");
        builder.UseSetting("EmailDispatcher:Smtp:FromAddress", "sender@example.com");
        builder.UseSetting("EmailDispatcher:Smtp:MaxAttempts", "1");
        builder.UseSetting("EmailDispatcher:Smtp:CircuitFailureThreshold", "1");
        builder.UseSetting("EmailDispatcher:Smtp:CircuitBreakSeconds", "5");
        builder.UseSetting("Identity:Verification:TokenLifetime", "00:00:20");
        builder.UseSetting("Outbox:PollInterval", "00:00:00.100");
        builder.ConfigureServices(services =>
        {
            // Mailpit is a local, unauthenticated test sink; retain the production validator with a development environment.
            var descriptor = services.Single(service => service.ImplementationType?.FullName == "BookStore.EmailDispatcher.SmtpSettingsValidator");
            services.Remove(descriptor);
            var environment = Substitute.For<IHostEnvironment>();
            environment.EnvironmentName.Returns(Environments.Development);
            services.AddSingleton(descriptor.ServiceType, Activator.CreateInstance(descriptor.ImplementationType!, environment)!);
        });
    }

    public override async ValueTask InitializeAsync()
    {
        await _database.StartAsync();
        await _mailpit.StartAsync();
        await base.InitializeAsync();
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.MigrateAsync();
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _mailpit.DisposeAsync();
        await _database.DisposeAsync();
    }
}

public sealed class VerificationOutboxEndToEndTests(VerificationOutboxEndToEndFactory factory)
    : IClassFixture<VerificationOutboxEndToEndFactory>
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
