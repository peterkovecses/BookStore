namespace BookStore.Catalog.IntegrationTests.Infrastructure;

[Collection("Catalog")]
public abstract class CatalogIntegrationTestBase(CatalogWebApplicationFactory factory) 
    : GenericIntegrationTestBase<CatalogWebApplicationFactory>(factory)
{
}
