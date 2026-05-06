namespace BookStore.Catalog.IntegrationTests.Infrastructure;

public class CatalogWebApplicationFactory : MsSqlWebApplicationFactory<Program, DbContext>
{
    private readonly AzuriteContainer _azuriteContainer = new AzuriteBuilder("mcr.microsoft.com/azure-storage/azurite:latest")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.UseSetting("ConnectionStrings:book-covers", _azuriteContainer.GetConnectionString());
    }

    protected override void ConfigureDatabase(IServiceCollection services)
    {
        services.RemoveAll<DbContextOptions<CatalogDbContext>>();
        services.AddDbContext<CatalogDbContext>(options =>
            options.UseSqlServer(GetConnectionString()));
    }

    protected override void ConfigureCustomServices(IServiceCollection services)
    {
        // No mocks here anymore, using real Azurite
    }

    protected override async Task InitializeDatabaseAsync(IServiceProvider serviceProvider)
    {
        var dbContext = serviceProvider.GetRequiredService<CatalogDbContext>();
        await dbContext.Database.MigrateAsync();

        await SeedDataAsync(serviceProvider);
    }

    protected override async Task SeedDataAsync(IServiceProvider serviceProvider)
    {
        var dbContext = serviceProvider.GetRequiredService<CatalogDbContext>();
        var blobServiceClient = serviceProvider.GetRequiredService<BlobServiceClient>();
        
        await CatalogTestSeeder.SeedAsync(dbContext, blobServiceClient);
    }

    public override async ValueTask InitializeAsync()
    {
        await _azuriteContainer.StartAsync();
        Environment.SetEnvironmentVariable("ConnectionStrings__book-covers", _azuriteContainer.GetConnectionString());
        
        await base.InitializeAsync();
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _azuriteContainer.DisposeAsync();
    }
}
