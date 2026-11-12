namespace BookStore.Identity.Common.PasswordRecovery;

internal static class PasswordRecoveryRateLimiting
{
    internal const string Forgot = "identity-password-forgot";
    internal const string Reset = "identity-password-reset";

    private const int DefaultForgotPermitLimit = 3;
    private const int DefaultResetPermitLimit = 10;
    private const int DefaultWindowSeconds = 60;
    private const int QueueLimit = 0;
    private const string ConfigurationPath = "Identity:PasswordRecovery:RateLimiting";

    internal static IServiceCollection AddPasswordRecoveryRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var forgot = ReadPolicy(configuration.GetSection($"{ConfigurationPath}:Forgot"), DefaultForgotPermitLimit);
        var reset = ReadPolicy(configuration.GetSection($"{ConfigurationPath}:Reset"), DefaultResetPermitLimit);
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(Forgot, context => Partition(context, forgot));
            options.AddPolicy(Reset, context => Partition(context, reset));
        });

        return services;
    }

    private static RateLimitPartition<string> Partition(HttpContext context, (int PermitLimit, int WindowSeconds) policy) =>
        RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ =>
            new FixedWindowRateLimiterOptions
            {
                PermitLimit = policy.PermitLimit,
                Window = TimeSpan.FromSeconds(policy.WindowSeconds),
                QueueLimit = QueueLimit,
                AutoReplenishment = true,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst
            });

    private static (int PermitLimit, int WindowSeconds) ReadPolicy(IConfigurationSection section, int defaultPermitLimit)
    {
        var permitLimit = section.GetValue<int?>("PermitLimit") ?? defaultPermitLimit;
        var windowSeconds = section.GetValue<int?>("WindowSeconds") ?? DefaultWindowSeconds;
        if (permitLimit < 1 || windowSeconds < 1)
        {
            throw new InvalidOperationException(
                $"Rate limits under '{section.Path}' must have positive PermitLimit and WindowSeconds values.");
        }

        return (permitLimit, windowSeconds);
    }
}
