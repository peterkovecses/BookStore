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
using Testcontainers.MsSql;

namespace BookStore.Identity.IntegrationTests;

public sealed class IdentityFactory : BaseWebApplicationFactory<Program>
{
    private readonly MsSqlContainer _database = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
    public RecordingIdGenerator Ids { get; } = new();
    public RecordingLoggerProvider Logs { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("ConnectionStrings:BookStoreDb", _database.GetConnectionString());
        builder.UseSetting("ConnectionStrings:book-covers", "UseDevelopmentStorage=true");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IUserIdGenerator>();
            services.AddSingleton<IUserIdGenerator>(Ids);
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

public sealed class RecordingIdGenerator : IUserIdGenerator
{
    public ConcurrentBag<string> Generated { get; } = [];
    public string Create()
    {
        var id = Guid.NewGuid().ToString();
        Generated.Add(id);

        return id;
    }
}

public class RegistrationTests(IdentityFactory factory) : IClassFixture<IdentityFactory>
{
    private static RegistrationRequest Request(string email, string password = "Password1!") => new()
    {
        Email = email, Password = password, FirstName = "Jane", LastName = "Doe"
    };

    [Fact]
    public async Task RegistrationPersistsUnconfirmedUserAndMakesDuplicatesIndistinguishable()
    {
        using var client = factory.CreateClient();
        var email = $"{Guid.NewGuid()}@example.com";
        var created = await client.PostAsJsonAsync("/api/identity/registration", Request(email), TestContext.Current.CancellationToken);
        var duplicate = await client.PostAsJsonAsync("/api/identity/registration", Request(email.ToUpperInvariant(), "OtherPass2!"), TestContext.Current.CancellationToken);

        created.StatusCode.Should().Be(HttpStatusCode.NoContent);
        duplicate.StatusCode.Should().Be(created.StatusCode);
        (await created.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))
            .Should().Be(await duplicate.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var user = await db.Users.SingleAsync(u => u.NormalizedEmail == email.ToUpperInvariant(), TestContext.Current.CancellationToken);
        factory.Ids.Generated.Should().Contain(user.Id);
        user.EmailConfirmed.Should().BeFalse();
        user.FirstName.Should().Be("Jane");
        user.LastName.Should().Be("Doe");
        user.PasswordHash.Should().NotBe("Password1!");
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        (await users.CheckPasswordAsync(user, "Password1!")).Should().BeTrue();
        (await users.CheckPasswordAsync(user, "OtherPass2!")).Should().BeFalse();
    }

    [Fact]
    public async Task ConcurrentDuplicateRequestsCreateOneUser()
    {
        using var client = factory.CreateClient();
        var email = $"{Guid.NewGuid()}@example.com";
        var responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ =>
            client.PostAsJsonAsync("/api/identity/registration", Request(email), TestContext.Current.CancellationToken)));
        responses.Should().OnlyContain(response => response.StatusCode == HttpStatusCode.NoContent);
        var bodies = await Task.WhenAll(responses.Select(response =>
            response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)));
        bodies.Distinct().Should().ContainSingle();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        (await db.Users.CountAsync(u => u.NormalizedEmail == email.ToUpperInvariant(), TestContext.Current.CancellationToken)).Should().Be(1);
    }

    [Fact]
    public async Task DatabaseEnforcesNormalizedEmailUniqueness()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var email = $"{Guid.NewGuid()}@example.com".ToUpperInvariant();
        db.Users.AddRange(
            new ApplicationUser { Id = Guid.NewGuid().ToString(), FirstName = "A", LastName = "B", NormalizedEmail = email },
            new ApplicationUser { Id = Guid.NewGuid().ToString(), FirstName = "A", LastName = "B", NormalizedEmail = email });
        var save = () => db.SaveChangesAsync(TestContext.Current.CancellationToken);
        await save.Should().ThrowAsync<DbUpdateException>();
    }

    [Theory]
    [InlineData("invalid", "Password1!", "Jane")]
    [InlineData("jane@example.com", "password", "Jane")]
    [InlineData("jane@example.com", "Password1!", "")]
    public async Task InvalidInputReturnsValidationProblem(string email, string password, string firstName)
    {
        using var client = factory.CreateClient();
        var request = new RegistrationRequest { Email = email, Password = password, FirstName = firstName, LastName = "Doe" };
        var response = await client.PostAsJsonAsync("/api/identity/registration", request, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body.Should().NotContain(password);
    }

    [Fact]
    public async Task MissingFieldsUseStandardValidationAndPasswordsAreNotLogged()
    {
        using var client = factory.CreateClient();
        const string password = "UniqueSecret42!";
        var response = await client.PostAsJsonAsync("/api/identity/registration",
            new { email = $"{Guid.NewGuid()}@example.com", password }, TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        body.Should().Contain("FirstName").And.Contain("LastName").And.NotContain(password);
        var successful = await client.PostAsJsonAsync("/api/identity/registration",
            Request($"{Guid.NewGuid()}@example.com", password), TestContext.Current.CancellationToken);
        successful.StatusCode.Should().Be(HttpStatusCode.NoContent);
        factory.Logs.Messages.Should().NotBeEmpty().And.NotContain(message => message.Contains(password, StringComparison.Ordinal));
    }

    [Fact]
    public async Task IdentityMigrationWasApplied()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var applied = await db.Database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken);
        applied.Should().ContainSingle(migration => migration.EndsWith("_InitialIdentity", StringComparison.Ordinal));
        (await db.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken)).Should().BeEmpty();
    }

    [Fact]
    public async Task ConfiguredPasswordPolicyControlsValidationAndIdentity()
    {
        await using var configuredFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Identity:Password:RequiredLength", "12");
            builder.UseSetting("Identity:Password:RequireDigit", "false");
        });
        using var client = configuredFactory.CreateClient();
        var shortPassword = await client.PostAsJsonAsync("/api/identity/registration",
            Request($"{Guid.NewGuid()}@example.com", "Short1!"), TestContext.Current.CancellationToken);
        var longPassword = await client.PostAsJsonAsync("/api/identity/registration",
            Request($"{Guid.NewGuid()}@example.com", "LongPassword!"), TestContext.Current.CancellationToken);

        shortPassword.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        longPassword.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using var scope = configuredFactory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        users.Options.Password.RequiredLength.Should().Be(12);
        users.Options.Password.RequireDigit.Should().BeFalse();
    }
}
