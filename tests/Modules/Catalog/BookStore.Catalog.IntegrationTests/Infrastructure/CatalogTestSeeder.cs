namespace BookStore.Catalog.IntegrationTests.Infrastructure;

internal static class CatalogTestSeeder
{
    public static async Task SeedAsync(CatalogDbContext dbContext, BlobServiceClient blobServiceClient)
    {
        await SeedDatabaseAsync(dbContext);
        await SeedStorageAsync(blobServiceClient);
    }

    private static async Task SeedDatabaseAsync(CatalogDbContext context)
    {
        var authors = new List<Author>
        {
            new() { Name = "Author 1" },
            new() { Name = "Author 2" }
        };

        var tags = new List<Tag>
        {
            new() { Id = "Tag 1" },
            new() { Id = "Tag 2" }
        };

        context.Authors.AddRange(authors);
        context.Tags.AddRange(tags);

        await context.SaveChangesAsync();
    }

    private static async Task SeedStorageAsync(BlobServiceClient blobServiceClient)
    {
        var containerClient = blobServiceClient.GetBlobContainerClient("book-covers");
        await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);
    }
}
