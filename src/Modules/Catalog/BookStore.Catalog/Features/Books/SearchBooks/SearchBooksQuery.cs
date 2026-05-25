namespace BookStore.Catalog.Features.Books.SearchBooks;

internal record SearchBooksQuery(
    SearchBooksFilter Filter,
    IPagination Pagination,
    ISorting Sorting) : IRequest<Result<PaginatedList<BookSummaryDto>>>;
