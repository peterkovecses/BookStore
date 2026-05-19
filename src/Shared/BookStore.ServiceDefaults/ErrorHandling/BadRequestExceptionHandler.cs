namespace BookStore.ServiceDefaults.ErrorHandling;

public sealed class BadRequestExceptionHandler(
    ILogger<BadRequestExceptionHandler> logger,
    IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not BadHttpRequestException badRequestException)
        {
            return false;
        }

        logger.LogBadRequestException(exception, exception.Message);

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                Title = "Bad Request",
                Detail = exception.Message
            }
        });
    }
}

internal static partial class BadRequestExceptionHandlerLogger
{
    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Bad request exception occurred: {Message}")]
    internal static partial void LogBadRequestException(this ILogger<BadRequestExceptionHandler> logger, Exception exception, string message);
}
