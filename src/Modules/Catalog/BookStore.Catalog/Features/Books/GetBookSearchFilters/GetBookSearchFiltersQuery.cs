namespace BookStore.Catalog.Features.Books.GetBookSearchFilters;

internal record GetBookSearchFiltersQuery() : IRequest<Result<BookSearchFiltersDto>>;
