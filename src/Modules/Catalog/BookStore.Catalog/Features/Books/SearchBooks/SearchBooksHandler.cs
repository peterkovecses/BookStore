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
            .Where(request.Filter.GetPredicate());

        if (string.Equals(request.Sorting.SortBy, "AverageRating", StringComparison.OrdinalIgnoreCase))
        {
            query = request.Sorting.IsDescending
                ? query.OrderByDescending(b => b.Reviews.Any() ? (double?)b.Reviews.Average(r => r.Stars) : null)
                : query.OrderBy(b => b.Reviews.Any() ? (double?)b.Reviews.Average(r => r.Stars) : null);
        }
        else if (string.Equals(request.Sorting.SortBy, "Price", StringComparison.OrdinalIgnoreCase))
        {
            query = request.Sorting.IsDescending
                ? query.OrderByDescending(b => b.Promotion != null ? b.Promotion.NewPrice : b.Price)
                : query.OrderBy(b => b.Promotion != null ? b.Promotion.NewPrice : b.Price);
        }
        else
        {
            query = query.ApplySorting(request.Sorting);
        }

        var result = await query
            .Select(BookMappings.ToBookSummary)
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
