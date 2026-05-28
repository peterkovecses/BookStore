namespace BookStore.UI.Common.Clients;

public interface IBookStoreApiClient
{
    Task<Result<PaginatedList<BookSummaryDto>>> SearchBooksAsync(
        BookSearchParameters searchParameters,
        IPagination pagination,
        ISorting sorting,
        CancellationToken cancellationToken);

    Task<Result<BookDetailsDto>> GetBookDetailsAsync(int id, CancellationToken cancellationToken);
    
    Task<Result<BookSearchFiltersDto>> GetSearchFiltersAsync(CancellationToken cancellationToken);
}
