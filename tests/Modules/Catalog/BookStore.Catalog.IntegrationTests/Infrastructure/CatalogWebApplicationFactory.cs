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

        await EnsureContainersExistAsync(serviceProvider);
    }

    public override async Task ResetDatabaseAsync()
    {
        await base.ResetDatabaseAsync();

        using var scope = Services.CreateScope();
        await ClearContainersAsync(scope.ServiceProvider);
    }

    private static async Task EnsureContainersExistAsync(IServiceProvider serviceProvider)
    {
        var blobServiceClient = serviceProvider.GetRequiredService<BlobServiceClient>();
        var containerClient = blobServiceClient.GetBlobContainerClient(CatalogTestConstants.BookCoversContainerName);
        await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);
    }

    private static async Task ClearContainersAsync(IServiceProvider serviceProvider)
    {
        var blobServiceClient = serviceProvider.GetRequiredService<BlobServiceClient>();
        var containerClient = blobServiceClient.GetBlobContainerClient(CatalogTestConstants.BookCoversContainerName);
        
        await foreach (var blobItem in containerClient.GetBlobsAsync())
        {
            await containerClient.DeleteBlobAsync(blobItem.Name);
        }
    }

    public override async ValueTask InitializeAsync()
    {
        await _azuriteContainer.StartAsync();
        
        await base.InitializeAsync();
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _azuriteContainer.DisposeAsync();
    }
}
