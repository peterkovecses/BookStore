namespace BookStore.Identity;

public static class DependencyInjection
{
    public static IHostApplicationBuilder AddIdentityModuleServices(this IHostApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString("BookStoreDb")
            ?? throw new InvalidOperationException("Connection string 'BookStoreDb' not found.");

        builder.Services.AddDbContext<IdentityDbContext>(options => options.UseSqlServer(connectionString,
            sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "identity")));
        builder.EnrichSqlServerDbContext<IdentityDbContext>();
        builder.Services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.User.AllowedUserNameCharacters = string.Empty;
            options.SignIn.RequireConfirmedEmail = true;
            options.Password.RequiredLength = 6;
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
        }).AddEntityFrameworkStores<IdentityDbContext>();

        builder.Services.AddSingleton<IUserIdGenerator, UserIdGenerator>();
        builder.Services.AddRequests(Assembly.GetExecutingAssembly());
        builder.Services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly(), includeInternalTypes: true);

        return builder;
    }

    public static async Task InitialiseIdentityModuleAsync(this IHost host)
    {
        if (!host.Services.GetRequiredService<IHostEnvironment>().IsDevelopment())
        {
            return;
        }

        using var scope = host.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.MigrateAsync();
    }

    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("api/identity").WithTags("Identity");
        RegistrationEndpoint.MapEndpoint(group);

        return app;
    }
}
