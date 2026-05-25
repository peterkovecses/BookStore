namespace BookStore.Catalog.Features.Books.GetBookSearchFilters;

internal sealed class GetBookSearchFiltersHandler(
    ICatalogDbContext dbContext,
    IMemoryCache cache)
    : IRequestHandler<GetBookSearchFiltersQuery, Result<BookSearchFiltersDto>>
{
    private const string CacheKey = "book_search_filters";

    public async Task<Result<BookSearchFiltersDto>> HandleAsync(
        GetBookSearchFiltersQuery request,
        CancellationToken cancellationToken)
    {
        var filters = await cache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(1);
            
            return await RetrieveFiltersAsync(cancellationToken);
        });

        return Result.Success(filters!);
    }

    private async Task<BookSearchFiltersDto> RetrieveFiltersAsync(CancellationToken cancellationToken)
    {
        var tagIds = await dbContext.Tags
            .AsNoTracking()
            .Select(tag => tag.Id)
            .ToListAsync(cancellationToken);

        var authorSorting = ISorting.Create(nameof(Author.Name), false);

        var authors = await dbContext.Authors
            .AsNoTracking()
            .ApplySorting(authorSorting)
            .Select(author => new AuthorDto
            {
                Id = author.Id,
                Name = author.Name
            })
            .ToListAsync(cancellationToken);

        return new BookSearchFiltersDto
        {
            TagIds = tagIds,
            Authors = authors
        };
    }
}
