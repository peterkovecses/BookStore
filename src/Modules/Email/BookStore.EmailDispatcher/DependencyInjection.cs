namespace BookStore.EmailDispatcher;

/// <summary>Provides service registration for the EmailDispatcher module.</summary>
public static class DependencyInjection
{
    /// <summary>Registers SMTP configuration, email delivery services, and email event handlers.</summary>
    /// <param name="builder">The host application builder to configure.</param>
    /// <returns>The same builder for chaining.</returns>
    public static IHostApplicationBuilder AddEmailDispatcherModuleServices(this IHostApplicationBuilder builder)
    {
        builder.Logging.AddFilter("Microsoft.AspNetCore.DataProtection", LogLevel.Warning);
        builder.Services.AddOptions<SmtpSettings>()
            .Bind(builder.Configuration.GetSection("EmailDispatcher:Smtp"))
            .ValidateOnStart();
        builder.Services.AddSingleton<IValidateOptions<SmtpSettings>, SmtpSettingsValidator>();
        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddScoped<EmailBodyRenderer>();
        builder.Services.AddSingleton<SmtpCircuitBreaker>();
        builder.Services.AddSingleton<IEmailDispatchAvailability>(sp => sp.GetRequiredService<SmtpCircuitBreaker>());
        builder.Services.AddSingleton<EmailSender>();
        builder.Services.AddSingleton<IEmailSender>(sp => sp.GetRequiredService<EmailSender>());
        builder.Services.AddScoped<IEventHandler<VerificationEmailRequested>, VerificationEmailRequestedHandler>();
        builder.Services.AddScoped<IEventHandler<PasswordResetEmailRequested>, PasswordResetEmailRequestedHandler>();

        return builder;
    }
}
