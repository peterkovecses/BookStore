namespace BookStore.Catalog.Features.Books.GetBookSearchFilters;

internal sealed class GetBookSearchFiltersHandler(ICatalogDbContext dbContext)
    : IRequestHandler<GetBookSearchFiltersQuery, Result<BookSearchFiltersDto>>
{
    public async Task<Result<BookSearchFiltersDto>> HandleAsync(
        GetBookSearchFiltersQuery request,
        CancellationToken cancellationToken)
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

        var filters = new BookSearchFiltersDto
        {
            TagIds = tagIds,
            Authors = authors
        };

        return Result.Success(filters);
    }
}
