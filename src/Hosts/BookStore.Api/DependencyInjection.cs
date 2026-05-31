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
                var uiUrl = configuration["services:bookstore-ui:https:0"] ?? 
                            configuration["services:bookstore-ui:http:0"];

                if (!string.IsNullOrEmpty(uiUrl))
                {
                    builder.WithOrigins(uiUrl)
                           .AllowAnyMethod()
                           .AllowAnyHeader();
                }
            });
        });

        return services;
    }
}
