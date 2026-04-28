namespace BookStore.Common.Logging;

public class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
    where TResponse : Result
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var startTime = Stopwatch.GetTimestamp();
        
        logger.LogStartingRequest(typeof(TRequest).Name);

        var result = await next(cancellationToken);

        if (result.IsFailure)
        {
            logger.LogRequestFailure(typeof(TRequest).Name, result.Errors!);
        }

        var elapsedTime = Stopwatch.GetElapsedTime(startTime);
        
        logger.LogCompletedRequest(typeof(TRequest).Name, elapsedTime);

        return result;
    }
}

internal static partial class LoggingBehaviorLogger
{
    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Starting request {RequestName}")]
    internal static partial void LogStartingRequest(this ILogger logger, string requestName);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Request failure {RequestName}, {@Errors}")]
    internal static partial void LogRequestFailure(this ILogger logger, string requestName, Error[] errors);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Completed request {RequestName}, ElapsedTime: {ElapsedTime}")]
    internal static partial void LogCompletedRequest(this ILogger logger, string requestName, TimeSpan elapsedTime);
}
