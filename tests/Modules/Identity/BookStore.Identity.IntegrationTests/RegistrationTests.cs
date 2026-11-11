namespace BookStore.Identity.IntegrationTests;

public sealed class IdentityFactory : BaseWebApplicationFactory<Program>
{
    private readonly MsSqlContainer _database = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
    public ConcurrentBag<string> GeneratedIds { get; } = [];
    public RecordingLoggerProvider Logs { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("ConnectionStrings:BookStoreDb", _database.GetConnectionString());
        builder.UseSetting("ConnectionStrings:book-covers", "UseDevelopmentStorage=true");
        builder.ConfigureServices(services =>
        {
            var idGenerator = Substitute.For<IUserIdGenerator>();
            idGenerator.Create().Returns(_ =>
            {
                var id = Guid.NewGuid().ToString();
                GeneratedIds.Add(id);

                return id;
            });
            services.RemoveAll<IUserIdGenerator>();
            services.AddSingleton(idGenerator);
        });
    }

    protected override void ConfigureCustomServices(IServiceCollection services)
    {
        services.RemoveAll<ILoggerFactory>();
        services.AddSingleton<ILoggerFactory>(_ => LoggerFactory.Create(logging =>
            logging.SetMinimumLevel(LogLevel.Trace).AddProvider(Logs)));
    }

    public sealed class RecordingLoggerProvider : ILoggerProvider
    {
        public ConcurrentQueue<string> Messages { get; } = new();
        public ILogger CreateLogger(string categoryName) => new RecordingLogger(Messages);
        public void Dispose() { }

        private sealed class RecordingLogger(ConcurrentQueue<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter) => messages.Enqueue(formatter(state, exception));
        }
    }

    public override async ValueTask InitializeAsync()
    {
        await _database.StartAsync();
        await base.InitializeAsync();
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.MigrateAsync();
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _database.DisposeAsync();
    }
}

public class RegistrationTests(IdentityFactory factory) : IClassFixture<IdentityFactory>
{
    private const string RegistrationPath = "/api/identity/registration";
    private const string DefaultPassword = "Password1!";
    private const string FirstName = "Jane";
    private const string LastName = "Doe";

    private static RegistrationRequest Request(string email, string password = DefaultPassword) => new()
    {
        Email = email, Password = password, FirstName = FirstName, LastName = LastName
    };

    [Fact]
    public async Task Register_WhenEmailAlreadyExists_ShouldPreserveAccountAndReturnSameResponse()
    {
        // Arrange
        const string duplicatePassword = "OtherPass2!";
        const HttpStatusCode expected = HttpStatusCode.NoContent;
        using var sut = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var email = $"{Guid.NewGuid()}@example.com";
        var normalizedEmail = email.ToUpperInvariant();

        // Act
        using var created = await sut.PostAsJsonAsync(RegistrationPath, Request(email), TestContext.Current.CancellationToken);
        using var actual = await sut.PostAsJsonAsync(RegistrationPath, Request(normalizedEmail, duplicatePassword), TestContext.Current.CancellationToken);
        var expectedBody = await created.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var actualBody = await actual.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var actualUser = await db.Users.SingleAsync(u => u.NormalizedEmail == normalizedEmail, TestContext.Current.CancellationToken);
        var actualOriginalPasswordMatches = await users.CheckPasswordAsync(actualUser, DefaultPassword);
        var actualDuplicatePasswordMatches = await users.CheckPasswordAsync(actualUser, duplicatePassword);

        // Assert
        created.StatusCode.Should().Be(expected);
        actual.StatusCode.Should().Be(created.StatusCode);
        actualBody.Should().Be(expectedBody);
        factory.GeneratedIds.Should().Contain(actualUser.Id);
        actualUser.EmailConfirmed.Should().BeFalse();
        actualUser.FirstName.Should().Be(FirstName);
        actualUser.LastName.Should().Be(LastName);
        actualUser.PasswordHash.Should().NotBe(DefaultPassword);
        actualOriginalPasswordMatches.Should().BeTrue();
        actualDuplicatePasswordMatches.Should().BeFalse();
    }

    [Fact]
    public async Task Register_WhenRequestsAreConcurrentAndDuplicate_ShouldCreateOneUser()
    {
        // Arrange
        const int requestCount = 5;
        const int expectedUserCount = 1;
        const HttpStatusCode expected = HttpStatusCode.NoContent;
        using var sut = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var email = $"{Guid.NewGuid()}@example.com";
        var normalizedEmail = email.ToUpperInvariant();

        // Act
        var actual = await Task.WhenAll(Enumerable.Range(0, requestCount).Select(_ =>
            sut.PostAsJsonAsync(RegistrationPath, Request(email), TestContext.Current.CancellationToken)));
        var actualBodies = await Task.WhenAll(actual.Select(response =>
            response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)));
        var actualUserCount = await db.Users.CountAsync(u => u.NormalizedEmail == normalizedEmail, TestContext.Current.CancellationToken);

        // Assert
        actual.Should().OnlyContain(response => response.StatusCode == expected);
        actualBodies.Distinct().Should().ContainSingle();
        actualUserCount.Should().Be(expectedUserCount);
        foreach (var response in actual) response.Dispose();
    }

    [Fact]
    public async Task SaveChangesAsync_WhenNormalizedEmailsAreDuplicate_ShouldRejectThem()
    {
        // Arrange
        const string firstName = "A";
        const string lastName = "B";
        using var scope = factory.Services.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var email = $"{Guid.NewGuid()}@example.com".ToUpperInvariant();
        sut.Users.AddRange(
            new ApplicationUser { Id = Guid.NewGuid().ToString(), FirstName = firstName, LastName = lastName, NormalizedEmail = email },
            new ApplicationUser { Id = Guid.NewGuid().ToString(), FirstName = firstName, LastName = lastName, NormalizedEmail = email });

        // Act
        var act = () => sut.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Register_WhenUsernameBelongsToAnotherEmail_ShouldNotReturnSuccessOrDiscloseAccount()
    {
        // Arrange
        const HttpStatusCode expected = HttpStatusCode.InternalServerError;
        using var sut = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var email = $"{Guid.NewGuid()}@example.com";
        var otherEmail = $"{Guid.NewGuid()}@example.com";
        db.Users.Add(new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(), FirstName = FirstName, LastName = LastName,
            Email = otherEmail, NormalizedEmail = otherEmail.ToUpperInvariant(),
            UserName = email, NormalizedUserName = email.ToUpperInvariant()
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Act
        using var actual = await sut.PostAsJsonAsync(RegistrationPath, Request(email), TestContext.Current.CancellationToken);
        var actualBody = await actual.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        actual.StatusCode.Should().Be(expected);
        actualBody.Should().NotContain(email).And.NotContain(otherEmail).And.NotContain("Duplicate");
    }

    [Fact]
    public async Task HandleAsync_WhenIdentityReturnsMixedErrors_ShouldReturnOnlyNonDuplicateErrors()
    {
        // Arrange
        const string expectedMessage = "Password is not acceptable.";
        const string duplicateMessage = "Sensitive duplicate detail";
        var email = $"{Guid.NewGuid()}@example.com";
        await using var configuredFactory = CreateFactoryWithUserValidation(IdentityResult.Failed(
            new IdentityError { Code = nameof(IdentityErrorDescriber.DuplicateUserName), Description = duplicateMessage },
            new IdentityError { Code = "PasswordRejected", Description = expectedMessage }));
        using var scope = configuredFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var ids = Substitute.For<IUserIdGenerator>();
        ids.Create().Returns(Guid.NewGuid().ToString());
        var sut = new RegistrationHandler(users, db, ids, scope.ServiceProvider.GetRequiredService<IVerificationRequestFactory>());

        // Act
        var actual = await sut.HandleAsync(new RegisterCommand(Request(email)), TestContext.Current.CancellationToken);

        // Assert
        actual.Should().BeFailure().HaveError("Validation").HaveValidationProperty("Registration");
        actual.Errors!.SelectMany(error => error.Metadata!.Values)
            .OfType<List<string>>().SelectMany(messages => messages)
            .Should().ContainSingle().Which.Should().Be(expectedMessage);
    }

    [Theory]
    [InlineData(nameof(IdentityErrorDescriber.DuplicateEmail))]
    [InlineData(nameof(IdentityErrorDescriber.DuplicateUserName))]
    public async Task HandleAsync_WhenIdentityReportsDuplicateAndEmailExists_ShouldReturnSuccess(string errorCode)
    {
        // Arrange
        var email = $"{Guid.NewGuid()}@example.com";
        var normalizedEmail = email.ToUpperInvariant();
        await using var configuredFactory = CreateFactoryWithUserValidation(IdentityResult.Failed(
            new IdentityError { Code = errorCode, Description = "Duplicate." }));
        using var scope = configuredFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var ids = Substitute.For<IUserIdGenerator>();
        ids.Create().Returns(Guid.NewGuid().ToString());
        db.Users.Add(new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(), FirstName = FirstName, LastName = LastName,
            Email = email, NormalizedEmail = normalizedEmail,
            UserName = email, NormalizedUserName = normalizedEmail
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.ChangeTracker.Clear();
        var sut = new RegistrationHandler(users, db, ids, scope.ServiceProvider.GetRequiredService<IVerificationRequestFactory>());
        var request = new RegisterCommand(Request(email));

        // Act
        var actual = await sut.HandleAsync(request, TestContext.Current.CancellationToken);

        // Assert
        actual.Should().BeSuccess();
    }

    [Theory]
    [InlineData(nameof(IdentityErrorDescriber.DuplicateEmail))]
    [InlineData(nameof(IdentityErrorDescriber.DuplicateUserName))]
    public async Task HandleAsync_WhenIdentityReportsDuplicateWithoutEmail_ShouldThrow(string errorCode)
    {
        // Arrange
        var email = $"{Guid.NewGuid()}@example.com";
        await using var configuredFactory = CreateFactoryWithUserValidation(IdentityResult.Failed(
            new IdentityError { Code = errorCode, Description = "Duplicate." }));
        using var scope = configuredFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var ids = Substitute.For<IUserIdGenerator>();
        ids.Create().Returns(Guid.NewGuid().ToString());
        var sut = new RegistrationHandler(users, db, ids, scope.ServiceProvider.GetRequiredService<IVerificationRequestFactory>());
        var request = new RegisterCommand(Request(email));

        // Act
        var act = () => sut.HandleAsync(request, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task HandleAsync_WhenDuplicateEmailIsDetectedByDatabase_ShouldReturnSuccessAndDetachUser()
    {
        // Arrange
        var email = $"{Guid.NewGuid()}@example.com";
        var normalizedEmail = email.ToUpperInvariant();
        await using var configuredFactory = CreateFactoryWithoutUserValidation();
        using var scope = configuredFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var ids = Substitute.For<IUserIdGenerator>();
        ids.Create().Returns(Guid.NewGuid().ToString());
        db.Users.Add(new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(), FirstName = FirstName, LastName = LastName,
            Email = email, NormalizedEmail = normalizedEmail,
            UserName = email, NormalizedUserName = normalizedEmail
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.ChangeTracker.Clear();
        var sut = new RegistrationHandler(users, db, ids, scope.ServiceProvider.GetRequiredService<IVerificationRequestFactory>());
        var request = new RegisterCommand(Request(email));

        // Act
        var actual = await sut.HandleAsync(request, TestContext.Current.CancellationToken);
        var actualCount = await db.Users.CountAsync(user => user.NormalizedEmail == normalizedEmail, TestContext.Current.CancellationToken);

        // Assert
        actual.Should().BeSuccess();
        actualCount.Should().Be(1);
        db.ChangeTracker.Entries<ApplicationUser>().Should().NotContain(entry => entry.State == EntityState.Added);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HandleAsync_WhenPrimaryKeyCollides_ShouldThrowEvenIfEmailExists(bool emailExists)
    {
        // Arrange
        var email = $"{Guid.NewGuid()}@example.com";
        var normalizedEmail = email.ToUpperInvariant();
        var otherEmail = $"{Guid.NewGuid()}@example.com";
        var collidingId = Guid.NewGuid().ToString();
        await using var configuredFactory = CreateFactoryWithoutUserValidation();
        using var scope = configuredFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var ids = Substitute.For<IUserIdGenerator>();
        ids.Create().Returns(collidingId);
        db.Users.Add(new ApplicationUser
        {
            Id = collidingId, FirstName = FirstName, LastName = LastName,
            Email = otherEmail, NormalizedEmail = otherEmail.ToUpperInvariant(),
            UserName = otherEmail, NormalizedUserName = otherEmail.ToUpperInvariant()
        });
        if (emailExists)
        {
            db.Users.Add(new ApplicationUser
            {
                Id = Guid.NewGuid().ToString(), FirstName = FirstName, LastName = LastName,
                Email = email, NormalizedEmail = normalizedEmail,
                UserName = email, NormalizedUserName = normalizedEmail
            });
        }
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.ChangeTracker.Clear();
        var sut = new RegistrationHandler(users, db, ids, scope.ServiceProvider.GetRequiredService<IVerificationRequestFactory>());
        var request = new RegisterCommand(Request(email));

        // Act
        var act = () => sut.HandleAsync(request, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();
        db.ChangeTracker.Entries<ApplicationUser>().Should().NotContain(entry => entry.State == EntityState.Added);
    }

    [Fact]
    public async Task HandleAsync_WhenUsernameBelongsToAnotherUserAndEmailExists_ShouldThrow()
    {
        // Arrange
        var email = $"{Guid.NewGuid()}@example.com";
        var normalizedEmail = email.ToUpperInvariant();
        var otherEmail = $"{Guid.NewGuid()}@example.com";
        var otherUsername = $"{Guid.NewGuid()}@example.com";
        await using var configuredFactory = CreateFactoryWithoutUserValidation();
        using var scope = configuredFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var ids = Substitute.For<IUserIdGenerator>();
        ids.Create().Returns(Guid.NewGuid().ToString());
        db.Users.AddRange(
            new ApplicationUser
            {
                Id = Guid.NewGuid().ToString(), FirstName = FirstName, LastName = LastName,
                Email = email, NormalizedEmail = normalizedEmail,
                UserName = otherUsername, NormalizedUserName = otherUsername.ToUpperInvariant()
            },
            new ApplicationUser
            {
                Id = Guid.NewGuid().ToString(), FirstName = FirstName, LastName = LastName,
                Email = otherEmail, NormalizedEmail = otherEmail.ToUpperInvariant(),
                UserName = email, NormalizedUserName = normalizedEmail
            });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.ChangeTracker.Clear();
        var sut = new RegistrationHandler(users, db, ids, scope.ServiceProvider.GetRequiredService<IVerificationRequestFactory>());
        var request = new RegisterCommand(Request(email));

        // Act
        var act = () => sut.HandleAsync(request, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();
        db.ChangeTracker.Entries<ApplicationUser>().Should().NotContain(entry => entry.State == EntityState.Added);
    }

    private WebApplicationFactory<Program> CreateFactoryWithoutUserValidation() =>
        CreateFactoryWithUserValidation(IdentityResult.Success);

    private WebApplicationFactory<Program> CreateFactoryWithUserValidation(IdentityResult result) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            var validator = Substitute.For<IUserValidator<ApplicationUser>>();
            validator.ValidateAsync(Arg.Any<UserManager<ApplicationUser>>(), Arg.Any<ApplicationUser>())
                .Returns(result);
            services.RemoveAll<IUserValidator<ApplicationUser>>();
            services.AddScoped(_ => validator);
        }));

    [Theory]
    [InlineData("invalid", "Password1!", "Jane")]
    [InlineData("jane@example.com", "password", "Jane")]
    [InlineData("jane@example.com", "Password1!", "")]
    public async Task Register_WhenInputIsInvalid_ShouldReturnValidationProblemWithoutPassword(string email, string password, string firstName)
    {
        // Arrange
        const HttpStatusCode expected = HttpStatusCode.BadRequest;
        using var sut = factory.CreateClient();
        var request = new RegistrationRequest { Email = email, Password = password, FirstName = firstName, LastName = LastName };

        // Act
        using var actual = await sut.PostAsJsonAsync(RegistrationPath, request, TestContext.Current.CancellationToken);
        var actualBody = await actual.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        actual.StatusCode.Should().Be(expected);
        actualBody.Should().NotContain(password);
    }

    [Fact]
    public async Task Register_WhenFieldsAreMissing_ShouldReturnValidationProblem()
    {
        // Arrange
        const string password = "UniqueSecret42!";
        const HttpStatusCode expected = HttpStatusCode.BadRequest;
        using var sut = factory.CreateClient();
        var request = new { email = $"{Guid.NewGuid()}@example.com", password };

        // Act
        using var actual = await sut.PostAsJsonAsync(RegistrationPath, request, TestContext.Current.CancellationToken);
        var actualBody = await actual.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        actual.StatusCode.Should().Be(expected);
        actualBody.Should().Contain("FirstName").And.Contain("LastName").And.NotContain(password);
        factory.Logs.Messages.Should().NotBeEmpty().And.NotContain(message => message.Contains(password, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"email\":null,\"password\":null,\"firstName\":null,\"lastName\":null}")]
    public async Task Register_WhenFieldsAreNullOrOmitted_ShouldReturnValidationProblem(string json)
    {
        // Arrange
        const HttpStatusCode expected = HttpStatusCode.BadRequest;
        const string expectedContentType = "application/problem+json";
        string[] expectedFields = ["Email", "Password", "FirstName", "LastName"];
        using var sut = factory.CreateClient();
        using var request = new StringContent(json, Encoding.UTF8, "application/json");

        // Act
        using var actual = await sut.PostAsync(RegistrationPath, request, TestContext.Current.CancellationToken);
        var actualBody = await actual.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        actual.StatusCode.Should().Be(expected);
        actual.Content.Headers.ContentType?.MediaType.Should().Be(expectedContentType);
        foreach (var expectedField in expectedFields)
        {
            actualBody.Should().Contain(expectedField);
        }
    }

    [Fact]
    public async Task Register_WhenPasswordIsProvided_ShouldNotLogIt()
    {
        // Arrange
        const string password = "UniqueSecret42!";
        const HttpStatusCode expected = HttpStatusCode.NoContent;
        using var sut = factory.CreateClient();
        var request = Request($"{Guid.NewGuid()}@example.com", password);

        // Act
        using var actual = await sut.PostAsJsonAsync(RegistrationPath, request, TestContext.Current.CancellationToken);

        // Assert
        actual.StatusCode.Should().Be(expected);
        factory.Logs.Messages.Should().NotBeEmpty().And.NotContain(message => message.Contains(password, StringComparison.Ordinal));
    }

    [Fact]
    public async Task MigrateAsync_WhenDatabaseIsInitialised_ShouldApplyIdentityMigration()
    {
        // Arrange
        const string migrationSuffix = "_InitialIdentity";
        using var scope = factory.Services.CreateScope();
        var sut = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();

        // Act
        var actual = await sut.Database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken);
        var actualPending = await sut.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken);

        // Assert
        actual.Should().ContainSingle(migration => migration.EndsWith(migrationSuffix, StringComparison.Ordinal));
        actualPending.Should().BeEmpty();
    }

    [Fact]
    public async Task Register_WhenPasswordPolicyIsConfigured_ShouldApplyItToValidationAndIdentity()
    {
        // Arrange
        const int expectedLength = 12;
        const string shortPassword = "Short1!";
        const string validPassword = "LongPassword!";
        const HttpStatusCode expectedInvalidStatus = HttpStatusCode.BadRequest;
        const HttpStatusCode expectedValidStatus = HttpStatusCode.NoContent;
        await using var configuredFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Identity:Password:RequiredLength", "12");
            builder.UseSetting("Identity:Password:RequireDigit", "false");
        });
        using var sut = configuredFactory.CreateClient();
        using var scope = configuredFactory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var invalidRequest = Request($"{Guid.NewGuid()}@example.com", shortPassword);
        var validRequest = Request($"{Guid.NewGuid()}@example.com", validPassword);

        // Act
        using var actualInvalid = await sut.PostAsJsonAsync(RegistrationPath, invalidRequest, TestContext.Current.CancellationToken);
        using var actualValid = await sut.PostAsJsonAsync(RegistrationPath, validRequest, TestContext.Current.CancellationToken);
        var actualOptions = users.Options.Password;

        // Assert
        actualInvalid.StatusCode.Should().Be(expectedInvalidStatus);
        actualValid.StatusCode.Should().Be(expectedValidStatus);
        actualOptions.RequiredLength.Should().Be(expectedLength);
        actualOptions.RequireDigit.Should().BeFalse();
    }
}
