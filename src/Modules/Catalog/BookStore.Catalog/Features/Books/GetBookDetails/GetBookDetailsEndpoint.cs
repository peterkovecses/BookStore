namespace BookStore.Catalog.Features.Books.GetBookDetails;

internal static class GetBookDetailsEndpoint
{
    internal static void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapGet("books/{id}", HandleAsync)
            .WithName("GetBookDetails")
            .WithDescription("Gets the details of a specific book.")
            .Produces<BookDetailsDto>()
            .ProducesValidationProblem();

    private static async Task<IResult> HandleAsync(
        string id,
        IRequestHandler<GetBookDetailsQuery, Result<BookDetailsDto>> handler,
        CancellationToken cancellationToken) => await handler.HandleAsync(new GetBookDetailsQuery(id), cancellationToken)
            .ToMinimalApiResultAsync();
}
