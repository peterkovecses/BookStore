using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace BookStore.Common.Outbox;

public static class OutboxDependencyInjection
{
    public static IServiceCollection AddOutbox<TStore>(this IServiceCollection services, IConfiguration configuration)
        where TStore : class, IOutboxStore
    {
        services.AddOptions<OutboxOptions>()
            .Bind(configuration.GetSection("Outbox"))
            .Validate(options => options.IsValid(), "Outbox configuration is invalid.")
            .ValidateOnStart();
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<OutboxRegistry<TStore>>();
        services.TryAddScoped<OutboxDispatchContext>();
        services.AddScoped<TStore>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, OutboxWorker<TStore>>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, OutboxCleaner<TStore>>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, OutboxMonitor<TStore>>());

        return services;
    }
}
