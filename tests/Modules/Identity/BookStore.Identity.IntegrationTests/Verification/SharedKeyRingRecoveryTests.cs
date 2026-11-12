namespace BookStore.Identity.IntegrationTests.Verification;

public sealed class SharedKeyRingRecoveryTests
{
    [Fact]
    public async Task ExecuteAsync_WhenProducerRestartsAndDeliveryRetries_ShouldDecryptAndConfirmAcrossInstances()
    {
        // Arrange
        const string registrationPath = "/api/identity/registration";
        const string confirmationPath = "/api/identity/verification/confirmation";
        const string certificatePassword = "test-only";
        var cancellationToken = TestContext.Current.CancellationToken;
        var keyDirectory = Path.Combine(Directory.GetCurrentDirectory(), ".data-protection-keys", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(keyDirectory);
        var certificatePath = Path.Combine(keyDirectory, "test-certificate.pfx");
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=BookStore recovery test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        File.WriteAllBytes(certificatePath, certificate.Export(X509ContentType.Pkcs12, certificatePassword));
        await using var database = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
        var sender = Substitute.For<IEmailSender>();
        var delivered = new TaskCompletionSource<EmailMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        var accepted = new ConcurrentQueue<EmailMessage>();
        var sendCount = 0;
        sender.SendAsync(Arg.Any<EmailMessage>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            accepted.Enqueue(call.Arg<EmailMessage>());
            if (Interlocked.Increment(ref sendCount) == 1)
            {
                return Task.FromException(new InvalidOperationException("Simulated lost SMTP acknowledgement"));
            }

            delivered.TrySetResult(call.Arg<EmailMessage>());

            return Task.CompletedTask;
        });

        try
        {
            await database.StartAsync(cancellationToken);
            var email = $"{Guid.NewGuid():N}@example.com";
            Guid eventId;
            string userId;
            string persistedPayload;
            await using (var producer = new RecoveryFactory(database.GetConnectionString(), keyDirectory, certificatePath, sender))
            {
                using var client = producer.CreateClient();
                using var scope = producer.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
                await db.Database.MigrateAsync(cancellationToken);
                using var response = await client.PostAsJsonAsync(registrationPath, new RegistrationRequest
                {
                    Email = email,
                    Password = "Password1!",
                    FirstName = "Jane",
                    LastName = "Doe"
                }, cancellationToken);
                response.StatusCode.Should().Be(HttpStatusCode.NoContent);
                var user = await db.Users.AsNoTracking().SingleAsync(item => item.Email == email, cancellationToken);
                var message = await db.OutboxMessages.AsNoTracking().SingleAsync(cancellationToken);
                userId = user.Id;
                eventId = message.Id;
                persistedPayload = message.Payload;
            }

            // Act
            await using (var retrying = new RecoveryFactory(database.GetConnectionString(), keyDirectory, certificatePath, sender))
            {
                using var scope = retrying.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
                using var worker = ActivatorUtilities.CreateInstance<OutboxWorker<IdentityOutboxStore>>(retrying.Services);
                try
                {
                    await worker.StartAsync(cancellationToken);
                    var failedAttempt = await WaitForMessageAsync(db, eventId,
                        message => message.Status == OutboxStatus.Pending && message.AttemptCount == 1 && message.NextAttemptAt is not null,
                        cancellationToken);
                    failedAttempt.CumulativeAttemptCount.Should().Be(1);
                }
                finally
                {
                    await worker.StopAsync(CancellationToken.None);
                }
            }

            await using var recovering = new RecoveryFactory(database.GetConnectionString(), keyDirectory, certificatePath, sender);
            using var recoveryScope = recovering.Services.CreateScope();
            var recoveryDb = recoveryScope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            using var recoveryWorker = ActivatorUtilities.CreateInstance<OutboxWorker<IdentityOutboxStore>>(recovering.Services);
            try
            {
                await recoveryWorker.StartAsync(cancellationToken);
                var completed = await WaitForMessageAsync(recoveryDb, eventId,
                    message => message.Status == OutboxStatus.Completed, cancellationToken);
                var emailMessage = await delivered.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
                var link = Regex.Match(emailMessage.TextBody, @"https://[^\s]+").Value;
                var query = QueryHelpers.ParseQuery(new Uri(link).Query);
                using var client = recovering.CreateClient();
                using var confirmation = await client.PostAsJsonAsync(confirmationPath, new
                {
                    UserId = query["userId"].ToString(),
                    Token = query["token"].ToString()
                }, cancellationToken);
                var confirmed = await recoveryDb.Users.AsNoTracking().SingleAsync(item => item.Id == userId, cancellationToken);
                var keyXml = File.ReadAllText(Directory.GetFiles(keyDirectory, "key-*.xml").Single());

                // Assert
                sendCount.Should().Be(2);
                accepted.Should().HaveCount(2);
                accepted.Select(message => message.TextBody).Distinct().Should().ContainSingle();
                completed.AttemptCount.Should().Be(2);
                completed.CumulativeAttemptCount.Should().Be(2);
                completed.Payload.Should().Be(persistedPayload);
                emailMessage.To.Should().Be(email);
                query["userId"].ToString().Should().Be(userId);
                query["token"].ToString().Should().NotBeNullOrEmpty();
                persistedPayload.Should().NotContain(link).And.NotContain(query["token"].ToString());
                keyXml.Should().Contain("encryptedSecret").And.NotContain("<masterKey");
                confirmation.StatusCode.Should().Be(HttpStatusCode.NoContent);
                confirmed.EmailConfirmed.Should().BeTrue();
            }
            finally
            {
                await recoveryWorker.StopAsync(CancellationToken.None);
            }
        }
        finally
        {
            if (Directory.Exists(keyDirectory))
            {
                Directory.Delete(keyDirectory, recursive: true);
            }
        }
    }

    private static async Task<OutboxMessage> WaitForMessageAsync(
        IdentityDbContext db, Guid id, Func<OutboxMessage, bool> predicate, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(45));
        while (true)
        {
            var message = await db.OutboxMessages.AsNoTracking().SingleAsync(item => item.Id == id, deadline.Token);
            if (predicate(message))
            {
                return message;
            }

            await Task.Delay(100, deadline.Token);
        }
    }

    private sealed class RecoveryFactory(
        string connectionString, string keyDirectory, string certificatePath, IEmailSender sender)
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("IntegrationTesting");
            builder.UseSetting("ConnectionStrings:BookStoreDb", connectionString);
            builder.UseSetting("ConnectionStrings:book-covers", "UseDevelopmentStorage=true");
            builder.UseSetting("DataProtection:ApplicationName", "BookStore.Api.RecoveryTests");
            builder.UseSetting("DataProtection:KeyRingPath", keyDirectory);
            builder.UseSetting("DataProtection:Certificate:Path", certificatePath);
            builder.UseSetting("DataProtection:Certificate:Password", "test-only");
            builder.UseSetting("Identity:Verification:LinkBaseUrl", "https://localhost/verify-email");
            builder.UseSetting("Identity:PasswordRecovery:LinkBaseUrl", "https://localhost/reset-password");
            builder.UseSetting("Identity:Authentication:Issuer", "https://api.example.test");
            builder.UseSetting("Identity:Authentication:Audience", "bookstore-test");
            builder.UseSetting("Identity:Authentication:SigningKey", Convert.ToBase64String(new byte[32]));
            builder.UseSetting("Identity:Authentication:AllowedOrigins:0", "https://ui.example.test");
            builder.UseSetting("EmailDispatcher:Smtp:Host", "localhost");
            builder.UseSetting("EmailDispatcher:Smtp:Username", "test-user");
            builder.UseSetting("EmailDispatcher:Smtp:Password", "test-password");
            builder.UseSetting("EmailDispatcher:Smtp:FromAddress", "sender@example.com");
            builder.UseSetting("Outbox:PollInterval", "00:00:00.100");
            builder.UseSetting("Outbox:RetryInitialDelay", "00:00:05");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IHostedService>();
                services.RemoveAll<IEmailSender>();
                services.AddSingleton(sender);
            });
        }
    }
}
