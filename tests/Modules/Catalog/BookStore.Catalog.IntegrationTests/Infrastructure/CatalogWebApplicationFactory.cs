namespace BookStore.Catalog.IntegrationTests.Infrastructure;

public class CatalogWebApplicationFactory : MsSqlWebApplicationFactory<Program, DbContext>
{
    private readonly AzuriteContainer _azuriteContainer = new AzuriteBuilder("mcr.microsoft.com/azure-storage/azurite:3.35.0")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.UseSetting("ConnectionStrings:book-covers", _azuriteContainer.GetConnectionString());
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<BlobServiceClient>();
            services.AddSingleton(new BlobServiceClient(
                _azuriteContainer.GetConnectionString(),
                new BlobClientOptions(BlobClientOptions.ServiceVersion.V2025_11_05)));
        });
    }

    protected override void ConfigureDatabase(IServiceCollection services)
    {
        services.RemoveAll<DbContextOptions<CatalogDbContext>>();
        services.AddDbContext<CatalogDbContext>(options =>
            options.UseSqlServer(GetConnectionString()));
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
        await ClearStorageAsync(scope.ServiceProvider);
    }

    private static async Task EnsureContainersExistAsync(IServiceProvider serviceProvider)
    {
        var blobServiceClient = serviceProvider.GetRequiredService<BlobServiceClient>();
        var containerClient = blobServiceClient.GetBlobContainerClient(CatalogTestConstants.BookCoversContainerName);
        await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);
    }

    private static async Task ClearStorageAsync(IServiceProvider serviceProvider)
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
