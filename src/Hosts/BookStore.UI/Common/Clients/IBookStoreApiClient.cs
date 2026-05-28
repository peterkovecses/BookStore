namespace BookStore.UI.Common.Clients;

public interface IBookStoreApiClient
{
    Task<Result<PaginatedList<BookSummaryDto>>> SearchBooksAsync(
        string? searchTerm,
        int[]? authorIds,
        string[]? tagIds,
        int pageNumber,
        int pageSize,
        string? sortBy,
        bool isDescending,
        CancellationToken cancellationToken);

    Task<Result<BookDetailsDto>> GetBookDetailsAsync(int id, CancellationToken cancellationToken);
    
    Task<Result<BookSearchFiltersDto>> GetSearchFiltersAsync(CancellationToken cancellationToken);
}
