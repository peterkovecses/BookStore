namespace BookStore.Api;

public static class DependencyInjection
{
    public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOpenApi();
        services.AddScoped<IOutboxPreflight<BookStore.Identity.Contracts.VerificationEmailRequested>,
            SmtpOutboxPreflight<BookStore.Identity.Contracts.VerificationEmailRequested>>();
        services.AddScoped<IOutboxPreflight<BookStore.Identity.Contracts.PasswordResetEmailRequested>,
            SmtpOutboxPreflight<BookStore.Identity.Contracts.PasswordResetEmailRequested>>();

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
