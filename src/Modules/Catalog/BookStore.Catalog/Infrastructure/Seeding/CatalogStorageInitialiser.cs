namespace BookStore.Catalog.Infrastructure.Seeding;

internal sealed class CatalogStorageInitialiser(
    BlobServiceClient blobServiceClient,
    ILogger<CatalogStorageInitialiser> logger)
{
    private const string ContainerName = "book-covers";

    public async Task SeedAsync()
    {
        try
        {
            var containerClient = blobServiceClient.GetBlobContainerClient(ContainerName);
            await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);
            logger.LogContainerEnsured(ContainerName);
            await UploadDefaultImagesAsync(containerClient);
        }
        catch (Exception ex)
        {
            logger.LogSeedingError(ex);
            throw;
        }
    }

    private async Task UploadDefaultImagesAsync(BlobContainerClient containerClient)
    {
        var assembly = typeof(CatalogStorageInitialiser).Assembly;
        var resourcePrefix = "BookStore.Catalog.Infrastructure.Seeding.Covers.";
        var resources = assembly.GetManifestResourceNames()
            .Where(r => r.StartsWith(resourcePrefix, StringComparison.OrdinalIgnoreCase));

        foreach (var resourceName in resources)
        {
            var fileName = resourceName[resourcePrefix.Length..];
            var blobClient = containerClient.GetBlobClient(fileName);

            if (await blobClient.ExistsAsync())
            {
                continue;
            }

            logger.LogUploadingImage(fileName);
            
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is not null)
            {
                await blobClient.UploadAsync(stream, new BlobHttpHeaders { ContentType = GetContentType(fileName) });
            }
        }
    }

    private static string GetContentType(string fileName) => 
        fileName.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) || 
        fileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) 
            ? "image/jpeg" 
            : "application/octet-stream";
}

internal static partial class CatalogStorageInitialiserLogging
{
    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Blob container {ContainerName} ensured.")]
    public static partial void LogContainerEnsured(this ILogger<CatalogStorageInitialiser> logger, string containerName);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Uploading default image: {FileName}")]
    public static partial void LogUploadingImage(this ILogger<CatalogStorageInitialiser> logger, string fileName);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "An error occurred while seeding the storage.")]
    public static partial void LogSeedingError(this ILogger<CatalogStorageInitialiser> logger, Exception ex);
}
