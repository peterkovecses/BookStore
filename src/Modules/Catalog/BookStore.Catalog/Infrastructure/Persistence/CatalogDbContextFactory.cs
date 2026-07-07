namespace BookStore.Catalog.Infrastructure.Persistence;

/// <summary>
/// Used by Entity Framework Core CLI design-time tools (e.g., dotnet ef migrations)
/// to instantiate the <see cref="CatalogDbContext"/> since this project is a class library.
/// This class is discovered automatically by EF Core tools via reflection.
/// </summary>
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
