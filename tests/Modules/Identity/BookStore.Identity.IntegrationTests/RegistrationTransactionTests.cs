namespace BookStore.Identity.IntegrationTests;

using Microsoft.AspNetCore.DataProtection;

public class RegistrationTransactionTests(IdentityFactory factory) : IClassFixture<IdentityFactory>
{
    private const string FirstName = "Jane";
    private const string LastName = "Doe";
    private const string Password = "Password1!";

    [Fact]
    public async Task HandleAsync_WhenRegistrationSucceeds_ShouldPersistUserAndVerificationEventInSameTransaction()
    {
        // Arrange
        var email = $"{Guid.NewGuid()}@example.com";
        var userId = Guid.NewGuid().ToString();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var ids = Substitute.For<IUserIdGenerator>();
        ids.Create().Returns(userId);
        var verificationRequests = Substitute.For<IVerificationRequestFactory>();
        var observedTransaction = false;
        var userVisibleInTransaction = false;
        verificationRequests.CreateAsync(Arg.Any<ApplicationUser>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                var user = call.Arg<ApplicationUser>();
                var cancellationToken = call.Arg<CancellationToken>();
                observedTransaction = db.Database.CurrentTransaction is not null;
                userVisibleInTransaction = await db.Users.AsNoTracking()
                    .AnyAsync(existing => existing.Id == user.Id, cancellationToken);

                return CreateOutboxMessage(user);
            });
        var sut = new RegistrationHandler(users, db, ids, verificationRequests);

        // Act
        var actual = await sut.HandleAsync(Command(email), TestContext.Current.CancellationToken);

        // Assert
        actual.Should().BeSuccess();
        observedTransaction.Should().BeTrue();
        userVisibleInTransaction.Should().BeTrue();
        using var verificationScope = factory.Services.CreateScope();
        var verification = verificationScope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        (await verification.Users.AsNoTracking()
            .AnyAsync(user => user.Id == userId, TestContext.Current.CancellationToken)).Should().BeTrue();
        var messages = await verification.OutboxMessages.AsNoTracking()
            .Where(message => message.CorrelationId == userId)
            .ToListAsync(TestContext.Current.CancellationToken);
        messages.Should().ContainSingle();
        messages[0].EventType.Should().Be(VerificationRequestFactory.EventType);
        var actualEvent = JsonSerializer.Deserialize<VerificationEmailRequested>(messages[0].Payload);
        actualEvent.Should().NotBeNull();
        actualEvent!.UserId.Should().Be(userId);
        actualEvent.Email.Should().Be(email);
        actualEvent.FirstName.Should().Be(FirstName);
    }

    [Fact]
    public async Task HandleAsync_WhenRetryingExecutionStrategyIsEnabled_ShouldPersistRealVerificationRequestInTransaction()
    {
        // Arrange
        var email = $"{Guid.NewGuid()}@example.com";
        var userId = Guid.NewGuid().ToString();
        await using var configuredFactory = CreateFactoryWithRetryEnabled();
        using var scope = configuredFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var actualVerificationRequests = scope.ServiceProvider.GetRequiredService<IVerificationRequestFactory>();
        var protection = scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>();
        var ids = Substitute.For<IUserIdGenerator>();
        ids.Create().Returns(userId);
        var observedTransaction = false;
        var userVisibleInTransaction = false;
        var actualMessageId = Guid.Empty;
        var verificationRequests = Substitute.For<IVerificationRequestFactory>();
        verificationRequests.CreateAsync(Arg.Any<ApplicationUser>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                var user = call.Arg<ApplicationUser>();
                var cancellationToken = call.Arg<CancellationToken>();
                observedTransaction = db.Database.CurrentTransaction is not null;
                userVisibleInTransaction = await db.Users.AsNoTracking()
                    .AnyAsync(existing => existing.Id == user.Id, cancellationToken);

                var message = await actualVerificationRequests.CreateAsync(user, cancellationToken);
                actualMessageId = message.Id;

                return message;
            });
        var sut = new RegistrationHandler(users, db, ids, verificationRequests);

        // Act
        var actual = await sut.HandleAsync(Command(email), TestContext.Current.CancellationToken);

        // Assert
        db.Database.CreateExecutionStrategy().RetriesOnFailure.Should().BeTrue();
        actual.Should().BeSuccess();
        observedTransaction.Should().BeTrue();
        userVisibleInTransaction.Should().BeTrue();
        using var verificationScope = configuredFactory.Services.CreateScope();
        var verification = verificationScope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        actualMessageId.Should().NotBeEmpty();
        (await verification.Users.AsNoTracking()
            .AnyAsync(user => user.Id == userId, TestContext.Current.CancellationToken)).Should().BeTrue();
        var actualMessage = await verification.OutboxMessages.AsNoTracking()
            .SingleAsync(message => message.Id == actualMessageId, TestContext.Current.CancellationToken);
        actualMessage.CorrelationId.Should().Be(actualMessage.Id.ToString());
        actualMessage.CorrelationId.Should().NotBe(userId);
        var actualEvent = JsonSerializer.Deserialize<VerificationEmailRequested>(actualMessage.Payload);
        actualEvent.Should().NotBeNull();
        actualEvent!.UserId.Should().Be(userId);
        actualEvent.Email.Should().Be(email);
        actualEvent.FirstName.Should().Be(FirstName);
        actualEvent.ProtectedLink.Should().NotBeNullOrWhiteSpace();
        var actualLink = protection.CreateProtector(VerificationRequestFactory.ProtectionPurpose)
            .CreateProtector(actualEvent.UserId)
            .CreateProtector(actualEvent.Email)
            .Unprotect(actualEvent.ProtectedLink);
        actualLink.Should().StartWith("https://localhost/verify-email?userId=");
        actualLink.Should().Contain(userId).And.Contain("token=");
    }

    [Fact]
    public async Task HandleAsync_WhenVerificationRequestCreationFails_ShouldRollbackUserAndOutboxMessage()
    {
        // Arrange
        const string failureMessage = "Verification request creation failed.";
        var email = $"{Guid.NewGuid()}@example.com";
        var userId = Guid.NewGuid().ToString();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var ids = Substitute.For<IUserIdGenerator>();
        ids.Create().Returns(userId);
        var verificationRequests = Substitute.For<IVerificationRequestFactory>();
        var observedTransaction = false;
        var userVisibleInTransaction = false;
        verificationRequests.CreateAsync(Arg.Any<ApplicationUser>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                var user = call.Arg<ApplicationUser>();
                var cancellationToken = call.Arg<CancellationToken>();
                observedTransaction = db.Database.CurrentTransaction is not null;
                userVisibleInTransaction = await db.Users.AsNoTracking()
                    .AnyAsync(existing => existing.Id == user.Id, cancellationToken);

                return await Task.FromException<OutboxMessage>(new InvalidOperationException(failureMessage));
            });
        var sut = new RegistrationHandler(users, db, ids, verificationRequests);

        // Act
        var act = () => sut.HandleAsync(Command(email), TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        observedTransaction.Should().BeTrue();
        userVisibleInTransaction.Should().BeTrue();
        using var verificationScope = factory.Services.CreateScope();
        var verification = verificationScope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        (await verification.Users.AsNoTracking()
            .AnyAsync(user => user.Id == userId, TestContext.Current.CancellationToken)).Should().BeFalse();
        (await verification.OutboxMessages.AsNoTracking()
            .AnyAsync(message => message.CorrelationId == userId, TestContext.Current.CancellationToken)).Should().BeFalse();
    }

    [Fact]
    public async Task HandleAsync_WhenIdentityCreationFails_ShouldRollbackWithoutCreatingVerificationEvent()
    {
        // Arrange
        var email = $"{Guid.NewGuid()}@example.com";
        var userId = Guid.NewGuid().ToString();
        var identityFailure = IdentityResult.Failed(new IdentityError { Code = "TestFailure", Description = "Identity creation failed." });
        var validator = Substitute.For<IUserValidator<ApplicationUser>>();
        await using var configuredFactory = CreateFactoryWithUserValidation(validator);
        using var scope = configuredFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var observedTransaction = false;
        var userVisibleInTransaction = false;
        validator.ValidateAsync(Arg.Any<UserManager<ApplicationUser>>(), Arg.Any<ApplicationUser>())
            .Returns(async call =>
            {
                var user = call.Arg<ApplicationUser>();
                observedTransaction = db.Database.CurrentTransaction is not null;
                db.Users.Add(user);
                await db.SaveChangesAsync(TestContext.Current.CancellationToken);
                userVisibleInTransaction = await db.Users.AsNoTracking()
                    .AnyAsync(existing => existing.Id == user.Id, TestContext.Current.CancellationToken);

                return identityFailure;
            });
        var ids = Substitute.For<IUserIdGenerator>();
        ids.Create().Returns(userId);
        var verificationRequests = Substitute.For<IVerificationRequestFactory>();
        var sut = new RegistrationHandler(users, db, ids, verificationRequests);

        // Act
        var actual = await sut.HandleAsync(Command(email), TestContext.Current.CancellationToken);

        // Assert
        actual.Should().BeFailure();
        observedTransaction.Should().BeTrue();
        userVisibleInTransaction.Should().BeTrue();
        await verificationRequests.DidNotReceive()
            .CreateAsync(Arg.Any<ApplicationUser>(), Arg.Any<CancellationToken>());
        using var verificationScope = configuredFactory.Services.CreateScope();
        var verification = verificationScope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        (await verification.Users.AsNoTracking()
            .AnyAsync(user => user.Id == userId, TestContext.Current.CancellationToken)).Should().BeFalse();
        (await verification.OutboxMessages.AsNoTracking()
            .AnyAsync(message => message.CorrelationId == userId, TestContext.Current.CancellationToken)).Should().BeFalse();
    }

    private static RegisterCommand Command(string email) => new(new RegistrationRequest
    {
        Email = email,
        Password = Password,
        FirstName = FirstName,
        LastName = LastName
    });

    private static OutboxMessage CreateOutboxMessage(ApplicationUser user)
    {
        var id = Guid.NewGuid();
        var value = new VerificationEmailRequested(
            user.Id,
            user.Email!,
            user.FirstName,
            "protected-link",
            DateTimeOffset.UtcNow.AddHours(24));

        return new OutboxMessage
        {
            Id = id,
            EventType = VerificationRequestFactory.EventType,
            Payload = JsonSerializer.Serialize(value),
            CorrelationId = user.Id,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    private WebApplicationFactory<Program> CreateFactoryWithUserValidation(IUserValidator<ApplicationUser> validator) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IUserValidator<ApplicationUser>>();
            services.AddScoped(_ => validator);
        }));

    private WebApplicationFactory<Program> CreateFactoryWithRetryEnabled()
    {
        using var scope = factory.Services.CreateScope();
        var connectionString = scope.ServiceProvider.GetRequiredService<IdentityDbContext>()
            .Database.GetConnectionString()!;

        return factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddDbContext<IdentityDbContext>(options => options.UseSqlServer(
                connectionString,
                sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "identity").EnableRetryOnFailure()))));
    }
}
