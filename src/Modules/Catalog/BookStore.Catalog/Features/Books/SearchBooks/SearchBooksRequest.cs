namespace BookStore.Catalog.Features.Books.SearchBooks;

public record SearchBooksRequest(
    string? SearchTerm = null,
    [FromQuery] int[]? AuthorIds = null,
    [FromQuery] string[]? TagIds = null,
    int PageNumber = IPagination.MinPageNumber,
    int PageSize = IPagination.DefaultPageSize,
    string? SortBy = null,
    bool IsDescending = false);
