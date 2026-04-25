namespace BookStore.Catalog;

public static class DependencyInjection
{
    public static IHostApplicationBuilder AddCatalogModuleServices(this IHostApplicationBuilder builder)
    {
        builder.Services.AddScoped<ISaveChangesInterceptor, AuditableEntityInterceptor>();
        builder.AddSqlServerDbContext<CatalogDbContext>("BookStoreDb");
        builder.AddAzureBlobServiceClient("book-covers");
        builder.Services.AddScoped<CatalogStorageInitialiser>();
        builder.Services.AddScoped<CatalogModuleInitialiser>();

        return builder;
    }
}
