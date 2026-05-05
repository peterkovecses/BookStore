using Azure.Storage.Blobs;
using BookStore.Catalog.Common.Interfaces;

namespace BookStore.Catalog.Infrastructure.Storage;

internal sealed class AzureCatalogStorageService(BlobServiceClient blobServiceClient) : ICatalogStorageService
{
    private const string ContainerName = "book-covers";

    public string GetPublicUrl(string imageName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imageName);

        return blobServiceClient
            .GetBlobContainerClient(ContainerName)
            .GetBlobClient(imageName)
            .Uri
            .ToString();
    }
}
