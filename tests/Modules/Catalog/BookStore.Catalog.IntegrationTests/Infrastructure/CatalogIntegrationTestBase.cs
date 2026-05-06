namespace BookStore.Catalog.IntegrationTests.Infrastructure;

[Collection("Catalog")]
public abstract class CatalogIntegrationTestBase(CatalogWebApplicationFactory factory) 
    : GenericIntegrationTestBase<CatalogWebApplicationFactory>(factory)
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
    /// Seeds books into the database, handling existing tags to prevent primary key violations.
    /// </summary>
    /// <param name="books">The collection of books to seed.</param>
    private protected async Task SeedBooksAsync(IEnumerable<Book> books)
    {
        await ExecuteDbContextAsync(async dbContext =>
        {
            foreach (var book in books)
            {
                var tags = book.Tags.ToList();
                book.Tags.Clear();
                foreach (var tag in tags)
                {
                    var existingTag = await dbContext.Tags.FindAsync(tag.Id);
                    book.Tags.Add(existingTag ?? tag);
                }

                dbContext.Books.Add(book);
            }

            await dbContext.SaveChangesAsync();
        });
    }

    /// <summary>
    /// Uploads a dummy image to the book-covers blob container.
    /// </summary>
    /// <param name="imageName">The name of the image file to upload.</param>
    private protected async Task UploadImageAsync(string imageName)
    {
        var blobServiceClient = Factory.Services.GetRequiredService<BlobServiceClient>();
        var containerClient = blobServiceClient.GetBlobContainerClient("book-covers");
        var dummyImageContent = new BinaryData([0x89, 0x50, 0x4E, 0x47]);
        
        await containerClient.GetBlobClient(imageName).UploadAsync(dummyImageContent.ToStream(), overwrite: true);
    }
}
