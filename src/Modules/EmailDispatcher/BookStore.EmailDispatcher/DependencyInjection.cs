using BookStore.EmailDispatcher.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace BookStore.EmailDispatcher;

public static class DependencyInjection
{
    public static IHostApplicationBuilder AddEmailDispatcherModuleServices(this IHostApplicationBuilder builder)
    {
        builder.Services.AddOptions<SmtpSettings>()
            .Bind(builder.Configuration.GetSection("EmailDispatcher:Smtp"))
            .ValidateOnStart();
        builder.Services.AddSingleton<IValidateOptions<SmtpSettings>, SmtpSettingsValidator>();
        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<SmtpCircuitBreaker>();
        builder.Services.AddSingleton<IEmailDispatchAvailability>(sp => sp.GetRequiredService<SmtpCircuitBreaker>());
        builder.Services.AddSingleton<EmailSender>();
        builder.Services.AddSingleton<IEmailSender>(sp => sp.GetRequiredService<EmailSender>());

        return builder;
    }
}
