namespace BookStore.Common;

public static class DependencyInjection
{
    public static MediatRServiceConfiguration AddValidationBehavior(this MediatRServiceConfiguration config)
    {
        config.AddOpenBehavior(typeof(ValidationBehavior<,>));
        
        return config;
    }

    public static MediatRServiceConfiguration AddLoggingBehavior(this MediatRServiceConfiguration config)
    {
        config.AddOpenBehavior(typeof(LoggingBehavior<,>));

        return config;
    }
}
