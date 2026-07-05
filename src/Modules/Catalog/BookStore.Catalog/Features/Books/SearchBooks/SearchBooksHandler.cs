namespace BookStore.Catalog.Features.Books.SearchBooks;

internal sealed class SearchBooksHandler(
    ICatalogDbContext dbContext,
    ICatalogStorageService storageService)
    : IRequestHandler<SearchBooksQuery, Result<PaginatedList<BookSummaryDto>>>
{
    public async Task<Result<PaginatedList<BookSummaryDto>>> HandleAsync(
        SearchBooksQuery request,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Books
            .AsNoTracking()
            .AsExpandable()
            .Where(request.Filter.GetPredicate())
            .Select(BookMappings.ToBookSummary)
            .ApplySorting(request.Sorting);

        var result = await query
            .ToPaginatedListAsync(request.Pagination, cancellationToken);

        PopulateImageUrls(result.Items);

        return Result.Success(result);
    }

    private void PopulateImageUrls(IEnumerable<BookSummaryDto> books)
    {
        foreach (var book in books)
        {
            if (!string.IsNullOrWhiteSpace(book.ImageName))
            {
                book.ImageUrl = storageService.GetPublicUrl(book.ImageName);
            }
        }
    }
}
