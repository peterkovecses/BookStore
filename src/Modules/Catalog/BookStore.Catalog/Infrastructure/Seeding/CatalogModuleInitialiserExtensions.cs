namespace BookStore.Catalog.Infrastructure.Seeding;

public static class CatalogModuleInitialiserExtensions
{
    public static async Task InitialiseCatalogModuleAsync(this IHost host)
    {
        using var scope = host.Services.CreateScope();        
        var initialiser = scope.ServiceProvider.GetRequiredService<CatalogModuleInitialiser>();        
        await initialiser.InitialiseAsync();
    }
}
