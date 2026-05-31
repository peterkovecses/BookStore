namespace BookStore.Api;

public static class DependencyInjection
{
    public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOpenApi();

        services.AddCors(options =>
        {
            options.AddDefaultPolicy(builder =>
            {
                var allowedOrigins = configuration["AllowedOrigins"];

                if (!string.IsNullOrEmpty(allowedOrigins))
                {
                    builder.WithOrigins(allowedOrigins)
                           .AllowAnyMethod()
                           .AllowAnyHeader();
                }
            });
        });

        return services;
    }
}
