using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace BookStore.Identity.Features.Verification;

internal static class VerificationRateLimitPolicies
{
    internal const string Confirmation = "identity-verification-confirmation";
    internal const string Resend = "identity-verification-resend";
}

internal static class VerificationRateLimiting
{
    private const string ConfigurationPath = "Identity:Verification:RateLimiting";

    internal static IServiceCollection AddVerificationRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var confirmation = ReadPolicy(configuration.GetSection($"{ConfigurationPath}:Confirmation"), 10);
        var resend = ReadPolicy(configuration.GetSection($"{ConfigurationPath}:Resend"), 3);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(VerificationRateLimitPolicies.Confirmation, context =>
                PartitionByIp(context, confirmation));
            options.AddPolicy(VerificationRateLimitPolicies.Resend, context =>
                PartitionByIp(context, resend));
        });

        return services;
    }

    private static RateLimitPartition<string> PartitionByIp(HttpContext context, FixedWindowPolicy policy)
    {
        var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(ipAddress, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = policy.PermitLimit,
            Window = TimeSpan.FromSeconds(policy.WindowSeconds),
            QueueLimit = 0,
            AutoReplenishment = true,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        });
    }

    private static FixedWindowPolicy ReadPolicy(IConfigurationSection section, int defaultPermitLimit)
    {
        var permitLimit = section.GetValue<int?>("PermitLimit") ?? defaultPermitLimit;
        var windowSeconds = section.GetValue<int?>("WindowSeconds") ?? 60;
        if (permitLimit < 1 || windowSeconds < 1)
        {
            throw new InvalidOperationException(
                $"Rate limits under '{section.Path}' must have positive PermitLimit and WindowSeconds values.");
        }

        return new(permitLimit, windowSeconds);
    }

    private sealed record FixedWindowPolicy(int PermitLimit, int WindowSeconds);
}
