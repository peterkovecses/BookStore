namespace BookStore.Catalog.Features.Books.GetBookSearchFilters;

internal static class GetBookSearchFiltersEndpoint
{
    internal static void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("books/filters", HandleAsync)
            .WithName("GetBookSearchFilters")
            .WithDescription("Gets the available authors and tags for book search filters.")
            .Produces<BookSearchFiltersDto>();

    private static async Task<IResult> HandleAsync(
        IRequestHandler<GetBookSearchFiltersQuery, Result<BookSearchFiltersDto>> handler,
        CancellationToken cancellationToken) =>
            await handler.HandleAsync(new GetBookSearchFiltersQuery(), cancellationToken)
                .ToMinimalApiResultAsync();
}
