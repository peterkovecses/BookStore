namespace BookStore.Catalog;

public static class DependencyInjection
{
    public static IHostApplicationBuilder AddCatalogModuleServices(this IHostApplicationBuilder builder)
    {
        builder.AddSqlServerDbContext<CatalogDbContext>("BookStoreDb");

        return builder;
    }
}
