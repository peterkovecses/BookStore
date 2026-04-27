namespace BookStore.Catalog;

public static class DependencyInjection
{
    public static IHostApplicationBuilder AddCatalogModuleServices(this IHostApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString("BookStoreDb")
            ?? throw new InvalidOperationException("Connection string 'BookStoreDb' not found.");

        builder.Services.AddScoped<AuditableEntityInterceptor>();
        builder.Services.AddDbContext<CatalogDbContext>((sp, options) =>
        {
            options.UseSqlServer(connectionString);
            options.AddInterceptors(sp.GetRequiredService<AuditableEntityInterceptor>());
        });

        builder.EnrichSqlServerDbContext<CatalogDbContext>();
        builder.AddAzureBlobServiceClient("book-covers");
        builder.Services.AddScoped<CatalogDbContextInitialiser>();
        builder.Services.AddScoped<CatalogStorageInitialiser>();
        builder.Services.AddScoped<CatalogModuleInitialiser>();

        return builder;
    }
}
