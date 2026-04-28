namespace BookStore.Api;

public static class DependencyInjection
{
    public static IServiceCollection AddApiServices(this IServiceCollection services)
    {
        services.AddOpenApi();

        services.AddMediatR(config =>
        {
            config.AddLoggingBehavior();
            config.AddValidationBehavior();
        });

        return services;
    }
}
