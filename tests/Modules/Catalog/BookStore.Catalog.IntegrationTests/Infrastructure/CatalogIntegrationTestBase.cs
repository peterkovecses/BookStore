namespace BookStore.Catalog.IntegrationTests.Infrastructure;

[Collection("Catalog Integration")]
public abstract class CatalogIntegrationTestBase(CatalogWebApplicationFactory factory)
    : IntegrationTestBase<Program, CatalogWebApplicationFactory>(factory)
{
    /// <summary>
    /// Executes an action with a scoped CatalogDbContext.
    /// </summary>
    /// <param name="action">The action to execute.</param>
    private protected async Task ExecuteDbContextAsync(Func<CatalogDbContext, Task> action)
    {
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        await action(dbContext);
    }

    /// <summary>
    /// Executes a function with a scoped CatalogDbContext and returns the result.
    /// </summary>
    /// <typeparam name="T">The type of the result.</typeparam>
    /// <param name="action">The function to execute.</param>
    /// <returns>The result of the function.</returns>
    private protected async Task<T> ExecuteDbContextAsync<T>(Func<CatalogDbContext, Task<T>> action)
    {
        using var scope = Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        
        return await action(dbContext);
    }

    /// <summary>
    /// Seeds one or more entities into the database.
    /// </summary>
    /// <param name="entities">The entities to seed.</param>
    private protected async Task SeedAsync(params object[] entities)
    {
        await ExecuteDbContextAsync(async db =>
        {
            foreach (var entity in entities)
            {
                if (entity is IEnumerable<object> collection)
                {
                    db.AddRange(collection);
                    continue;
                }
                db.Add(entity);
            }

            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
        });
    }

    /// <summary>
    /// Creates a book with default values for testing.
    /// </summary>
    private protected static Book CreateBook(
        string title = "Default Title",
        string description = "Default Description",
        string publisher = "Default Publisher",
        decimal price = 10.00m,
        bool softDeleted = false,
        string? imageName = null,
        DateTime? publishedOn = null,
        IEnumerable<Author>? authors = null,
        IEnumerable<Tag>? tags = null,
        PriceOffer? promotion = null,
        IEnumerable<Review>? reviews = null)
    {
        var book = new Book
        {
            Title = title,
            Description = description,
            Publisher = publisher,
            Price = price,
            SoftDeleted = softDeleted,
            ImageName = imageName,
            PublishedOn = publishedOn ?? new DateTime(2026, 5, 6, 0, 0, 0, DateTimeKind.Utc),
            Promotion = promotion
        };

        if (authors is not null)
        {
            foreach (var author in authors.Select((a, index) => new BookAuthor { Author = a, Order = (byte)(index + 1) }))
            {
                book.AuthorsLink.Add(author);
            }
        }

        if (tags is not null)
        {
            foreach (var tag in tags)
            {
                book.Tags.Add(tag);
            }
        }

        if (reviews is not null)
        {
            foreach (var review in reviews)
            {
                book.Reviews.Add(review);
            }
        }

        return book;
    }

    /// <summary>
    /// Uploads a dummy image to the book-covers blob container.
    /// </summary>
    /// <param name="imageName">The name of the image file to upload.</param>
    private protected async Task UploadImageAsync(string imageName)
    {
        var blobServiceClient = Factory.Services.GetRequiredService<BlobServiceClient>();
        var containerClient = blobServiceClient.GetBlobContainerClient(CatalogTestConstants.BookCoversContainerName);
        var dummyImageContent = new BinaryData([0x89, 0x50, 0x4E, 0x47]);

        await containerClient.GetBlobClient(imageName).UploadAsync(dummyImageContent.ToStream(), overwrite: true);
    }
}
