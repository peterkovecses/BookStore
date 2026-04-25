namespace BookStore.Catalog.Infrastructure.Seeding;

internal sealed class CatalogModuleInitialiser(
    CatalogStorageInitialiser storageInitialiser)
{
    public async Task InitialiseAsync()
    {
        await storageInitialiser.SeedAsync();
    }
}
