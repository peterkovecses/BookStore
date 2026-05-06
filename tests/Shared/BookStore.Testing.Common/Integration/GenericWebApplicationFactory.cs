namespace BookStore.Testing.Common.Integration;

/// <summary>
/// Generic WebApplicationFactory for integration testing with MSSQL container support.
/// Handles database initialization, respawning, and JWT authentication configuration.
/// </summary>
/// <typeparam name="TMarker">Assembly marker interface for API project identification</typeparam>
/// <typeparam name="TDbContext">The DbContext type to use for this API</typeparam>
public abstract class GenericWebApplicationFactory<TMarker, TDbContext> : WebApplicationFactory<TMarker>, IAsyncLifetime
    where TMarker : class
    where TDbContext : DbContext
{
    private readonly MsSqlContainer _dbContainer = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .WithPassword("Super_secret_password_123@")
        .Build();

    private DbConnection _dbConnection = null!;
    private Respawner _respawner = null!;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
        });

        builder.ConfigureTestServices(services =>
        {
            ConfigureDefaultServices(services);
            ConfigureDatabase(services);
            ConfigureAuthentication(services);
            ConfigureCustomServices(services);
        });
    }

    /// <summary>
    /// Default service configuration (removing hosted services).
    /// Override if you need different default behavior.
    /// </summary>
    protected virtual void ConfigureDefaultServices(IServiceCollection services) => services.RemoveAll<IHostedService>();

    /// <summary>
    /// Configure the database context. Concrete implementations should override this.
    /// </summary>
    protected abstract void ConfigureDatabase(IServiceCollection services);

    /// <summary>
    /// Configure JWT authentication. Concrete implementations can override for custom config.
    /// </summary>
    protected virtual void ConfigureAuthentication(IServiceCollection services)
    {
        services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            var key = Encoding.UTF8.GetBytes(JwtConstants.SecurityKey);
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = false,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key)
            };
        });
    }

    /// <summary>
    /// Configure custom services specific to the concrete implementation.
    /// Override this to add project-specific service configuration.
    /// </summary>
    protected virtual void ConfigureCustomServices(IServiceCollection services)
    {
        // Default: no custom services. Override in concrete implementations.
    }

    /// <summary>
    /// Gets the database connection string from the container.
    /// </summary>
    protected string GetConnectionString() => _dbContainer.GetConnectionString();

    /// <summary>
    /// Resets the database to a clean state for the next test.
    /// </summary>
    public async Task ResetDatabaseAsync()
    {
        if (_respawner is not null)
        {
            await _respawner.ResetAsync(_dbConnection);
        }
    }

    /// <summary>
    /// Initialize the test factory and database container.
    /// </summary>
    public async ValueTask InitializeAsync()
    {
        await _dbContainer.StartAsync();
        var connectionString = GetConnectionString();
        _dbConnection = new SqlConnection(connectionString);

        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();
        await dbContext.Database.MigrateAsync();

        await InitializeRespawnerAsync();
    }

    /// <summary>
    /// Initialize the Respawner for efficient database cleanup between tests.
    /// </summary>
    private async Task InitializeRespawnerAsync()
    {
        await _dbConnection.OpenAsync();
        _respawner = await Respawner.CreateAsync(
            _dbConnection,
            new RespawnerOptions
            {
                DbAdapter = DbAdapter.SqlServer
            });
    }

    /// <summary>
    /// Cleanup resources (database container).
    /// </summary>
    public new async ValueTask DisposeAsync()
    {
        await _dbContainer.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}