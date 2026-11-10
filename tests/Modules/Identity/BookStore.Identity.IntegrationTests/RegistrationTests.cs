using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using BookStore.Identity.Contracts;
using BookStore.Identity.Infrastructure;
using BookStore.Testing.Common.Integration;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Testcontainers.MsSql;

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
        using var request = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

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
