namespace BookStore.Catalog.Features.Books.SearchBooks;

internal sealed class SearchBooksHandler(
    ICatalogDbContext dbContext,
    BlobServiceClient blobServiceClient)
    : IRequestHandler<SearchBooksQuery, Result<PaginatedList<BookSummaryDto>>>
{
    private const string ContainerName = "book-covers";

    public async Task<Result<PaginatedList<BookSummaryDto>>> Handle(
        SearchBooksQuery request,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Books
            .AsNoTracking()
            .AsExpandable()
            .Where(request.Filter.GetPredicate())
            .ApplySorting(request);

        var result = await query
            .Select(BookMappings.ToBookSummary)
            .ToPaginatedListAsync(request, cancellationToken);

        PopulateImageUrls(result.Items);

        return Result.Success(result);
    }

    private void PopulateImageUrls(IEnumerable<BookSummaryDto> books)
    {
        var containerClient = blobServiceClient.GetBlobContainerClient(ContainerName);

        foreach (var book in books)
        {
            if (book.ImageName is not null)
            {
                book.ImageUrl = containerClient.GetBlobClient(book.ImageName).Uri.ToString();
            }
        }
    }
}
