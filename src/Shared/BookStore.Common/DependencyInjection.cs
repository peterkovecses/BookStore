namespace BookStore.Common;

public static class DependencyInjection
{
    public static IRequestsBuilder AddValidationBehavior(this IRequestsBuilder builder)
    {
        builder.AddGlobalBehavior(typeof(ValidationBehavior<,>));
        
        return builder;
    }

    public static IRequestsBuilder AddLoggingBehavior(this IRequestsBuilder builder)
    {
        builder.AddGlobalBehavior(typeof(LoggingBehavior<,>));

        return builder;
    }
}