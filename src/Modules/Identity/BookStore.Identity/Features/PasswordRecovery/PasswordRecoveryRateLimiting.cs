namespace BookStore.Identity.Features.PasswordRecovery;

internal static class PasswordRecoveryRateLimiting
{
    internal const string Forgot = "identity-password-forgot";
    internal const string Reset = "identity-password-reset";

    internal static IServiceCollection AddPasswordRecoveryRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var forgot = ReadPolicy(configuration.GetSection("Identity:PasswordRecovery:RateLimiting:Forgot"), 3);
        var reset = ReadPolicy(configuration.GetSection("Identity:PasswordRecovery:RateLimiting:Reset"), 10);
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(Forgot, context => Partition(context, forgot));
            options.AddPolicy(Reset, context => Partition(context, reset));
        });

        return services;
    }

    private static RateLimitPartition<string> Partition(HttpContext context, (int Limit, int Seconds) policy) =>
        RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ =>
            new FixedWindowRateLimiterOptions
            {
                PermitLimit = policy.Limit,
                Window = TimeSpan.FromSeconds(policy.Seconds),
                QueueLimit = 0,
                AutoReplenishment = true
            });

    private static (int Limit, int Seconds) ReadPolicy(IConfigurationSection section, int defaultLimit)
    {
        var limit = section.GetValue<int?>("PermitLimit") ?? defaultLimit;
        var seconds = section.GetValue<int?>("WindowSeconds") ?? 60;
        if (limit < 1 || seconds < 1)
        {
            throw new InvalidOperationException($"Rate limits under '{section.Path}' must be positive.");
        }

        return (limit, seconds);
    }
}
