namespace BookStore.Catalog.Infrastructure.Persistence;

internal class CatalogDbContextFactory : IDesignTimeDbContextFactory<CatalogDbContext>
{
    public CatalogDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<CatalogDbContext>();
        optionsBuilder.UseSqlServer();
        optionsBuilder.AddInterceptors(new AuditableEntityInterceptor(TimeProvider.System));

        return new CatalogDbContext(optionsBuilder.Options);
    }
}
