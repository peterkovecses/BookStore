namespace BookStore.Catalog.Features.Books.GetBookSearchFilters;

internal record GetBookSearchFiltersQuery() : IRequest<Result<BookSearchFiltersDto>>, ICachedQuery
{
    public string CacheKey => "book_search_filters";
    public TimeSpan? Expiration => TimeSpan.FromDays(1);
}
