namespace BookStore.Catalog.Contracts.Dtos;

public class BookSearchFiltersDto
{
    public IEnumerable<string> TagIds { get; init; } = [];
    public IEnumerable<AuthorDto> Authors { get; init; } = [];
}
