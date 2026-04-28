namespace BookStore.Catalog.Features.Books.SearchBooks;

internal record SearchBooksQuery(
    SearchBooksFilter Filter,
    int PageNumber,
    int PageSize,
    string? SortBy,
    bool IsDescending) : IRequest<Result<PaginatedList<BookSummaryDto>>>, IPagination, ISorting;
