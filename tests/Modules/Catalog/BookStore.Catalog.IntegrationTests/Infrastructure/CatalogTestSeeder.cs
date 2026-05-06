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
        var fixedDate = new DateTime(2026, 5, 6, 0, 0, 0, DateTimeKind.Utc);

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

        var books = new List<Book>
        {
            new()
            {
                Title = "Active Book With Image",
                Description = "Description 1",
                PublishedOn = fixedDate,
                Publisher = "Publisher 1",
                Price = 10.00m,
                ImageName = "image1.jpg",
                SoftDeleted = false,
                AuthorsLink = [new BookAuthor { Author = authors[0], Order = 1 }],
                Tags = [tags[0]]
            },
            new()
            {
                Title = "Active Book Without Image",
                Description = "Description 2",
                PublishedOn = fixedDate,
                Publisher = "Publisher 2",
                Price = 20.00m,
                ImageName = null,
                SoftDeleted = false,
                AuthorsLink = [new BookAuthor { Author = authors[1], Order = 1 }],
                Tags = [tags[1]]
            },
            new()
            {
                Title = "Soft Deleted Book",
                Description = "Description 3",
                PublishedOn = fixedDate,
                Publisher = "Publisher 3",
                Price = 30.00m,
                ImageName = "image3.jpg",
                SoftDeleted = true,
                AuthorsLink = [new BookAuthor { Author = authors[0], Order = 1 }]
            }
        };

        context.Authors.AddRange(authors);
        context.Tags.AddRange(tags);
        context.Books.AddRange(books);

        await context.SaveChangesAsync();
    }

    private static async Task SeedStorageAsync(BlobServiceClient blobServiceClient)
    {
        var containerClient = blobServiceClient.GetBlobContainerClient("book-covers");
        await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);

        // Upload dummy images for testing
        var dummyImageContent = new BinaryData([0x89, 0x50, 0x4E, 0x47]); // Dummy PNG header
        
        await containerClient.GetBlobClient("image1.jpg").UploadAsync(dummyImageContent.ToStream(), overwrite: true);
        await containerClient.GetBlobClient("image3.jpg").UploadAsync(dummyImageContent.ToStream(), overwrite: true);
    }
}
