namespace BookStore.Catalog.Features.Books.SearchBooks;

internal static class SearchBooksEndpoint
{
    internal static void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("books", HandleAsync)
            .WithName("SearchBooks")
            .WithDescription("Searches for books with filtering, sorting, and pagination.")
            .Produces<PaginatedList<BookSummaryDto>>()
            .ProducesValidationProblem();
    }

    private static async Task<IResult> HandleAsync(
        [AsParameters] SearchBooksRequest request,
        IRequestHandler<SearchBooksQuery, Result<PaginatedList<BookSummaryDto>>> handler,
        CancellationToken cancellationToken)
    {
        var query = new SearchBooksQuery(
            new SearchBooksFilter
            {
                SearchTerm = request.SearchTerm,
                AuthorIds = request.AuthorIds?.ToHashSet() ?? [],
                TagIds = request.TagIds?.ToHashSet() ?? []
            },
            request.PageNumber,
            request.PageSize,
            request.SortBy,
            request.IsDescending);

        return await handler.Handle(query, cancellationToken)
            .ToMinimalApiResultAsync();
    }
}
