namespace BookStore.Testing.Common.Integration;

/// <summary>
/// Specialized WebApplicationFactory for integration testing with EF Core and MSSQL support.
/// Handles database initialization, migrations, and respawning.
/// </summary>
/// <typeparam name="TMarker">Assembly marker interface for API project identification</typeparam>
/// <typeparam name="TDbContext">The DbContext type to use for this API</typeparam>
public abstract class MsSqlWebApplicationFactory<TMarker, TDbContext> : BaseWebApplicationFactory<TMarker>
    where TMarker : class
    where TDbContext : DbContext
{
    private readonly MsSqlContainer _dbContainer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
        .WithPassword("Super_secret_password_123@")
        .Build();

    private DbConnection _dbConnection = null!;
    private Respawner _respawner = null!;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        
        builder.UseSetting("ConnectionStrings:BookStoreDb", GetConnectionString());
        
        builder.ConfigureTestServices(services =>
        {
            ConfigureDatabase(services);
        });
    }

    /// <summary>
    /// Configure the database context. Concrete implementations should override this.
    /// </summary>
    protected abstract void ConfigureDatabase(IServiceCollection services);

    /// <summary>
    /// Gets the database connection string from the container.
    /// </summary>
    protected string GetConnectionString() => _dbContainer.GetConnectionString();

    /// <summary>
    /// Resets the database to a clean state and re-seeds it for the next test.
    /// </summary>
    public async Task ResetDatabaseAsync()
    {
        if (_respawner is not null)
        {
            await _respawner.ResetAsync(_dbConnection);

            using var scope = Services.CreateScope();
            await SeedDataAsync(scope.ServiceProvider);
        }
    }

    /// <summary>
    /// Initialize the test factory and database container.
    /// </summary>
    public override async ValueTask InitializeAsync()
    {
        await base.InitializeAsync();
        await _dbContainer.StartAsync();
        
        var connectionString = GetConnectionString();
        Environment.SetEnvironmentVariable("ConnectionStrings__BookStoreDb", connectionString);
        
        _dbConnection = new SqlConnection(connectionString);

        using var scope = Services.CreateScope();
        await InitializeDatabaseAsync(scope.ServiceProvider);

        await InitializeRespawnerAsync();
    }

    /// <summary>
    /// Initializes the database by applying migrations and seeding initial data.
    /// </summary>
    /// <param name="serviceProvider">The service provider to resolve dependencies.</param>
    protected virtual async Task InitializeDatabaseAsync(IServiceProvider serviceProvider)
    {
        var dbContext = serviceProvider.GetRequiredService<TDbContext>();
        await dbContext.Database.MigrateAsync();

        await SeedDataAsync(serviceProvider);
    }

    /// <summary>
    /// Seed the database with initial data before the Respawner snapshot is taken.
    /// </summary>
    /// <param name="serviceProvider">The service provider to resolve dependencies.</param>
    protected virtual Task SeedDataAsync(IServiceProvider serviceProvider) => Task.CompletedTask;

    /// <summary>
    /// Cleanup resources (database container and connection).
    /// </summary>
    public override async ValueTask DisposeAsync()
    {
        if (_dbConnection is not null)
        {
            if (_dbConnection.State == ConnectionState.Open)
            {
                await _dbConnection.CloseAsync();
            }
            _dbConnection.Dispose();
        }

        await _dbContainer.DisposeAsync();
        await base.DisposeAsync();
    }

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
}