namespace BookStore.Catalog.Infrastructure.Seeding;

internal sealed class CatalogModuleInitialiser(
    CatalogDbContextInitialiser dbInitialiser,
    CatalogStorageInitialiser storageInitialiser)
{
    public async Task InitialiseAsync()
    {
        await dbInitialiser.InitialiseAsync();
        await dbInitialiser.SeedAsync();
        await storageInitialiser.SeedAsync();
    }
}
