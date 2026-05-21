namespace BookStore.Api;

public static class DependencyInjection
{
    public static IServiceCollection AddApiServices(this IServiceCollection services)
    {
        services.AddOpenApi();

        services.AddRequests(Assembly.GetExecutingAssembly())
            .AddLoggingBehavior()
            .AddValidationBehavior();

        return services;
    }
}
