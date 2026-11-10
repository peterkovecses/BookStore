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
        }).AddEntityFrameworkStores<IdentityDbContext>();

        builder.Services.AddOptions<IdentityOptions>()
            .Configure(options => builder.Configuration.GetSection("Identity:Password").Bind(options.Password))
            .Validate(options => options.Password.RequiredLength >= 6,
                "Identity:Password:RequiredLength must be at least 6.")
            .Validate(options => options.Password.RequiredUniqueChars >= 1,
                "Identity:Password:RequiredUniqueChars must be at least 1.")
            .ValidateOnStart();

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
