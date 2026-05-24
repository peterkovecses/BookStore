namespace BookStore.Catalog.Common.Extensions;

public static class CatalogEndpoints
{
    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder app)
    {
        var catalogGroup = app.MapGroup("api/catalog")
            .WithTags("Catalog");

        SearchBooksEndpoint.MapEndpoint(catalogGroup);
        GetBookDetailsEndpoint.MapEndpoint(catalogGroup);

        return app;
    }
}
