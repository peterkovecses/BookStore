namespace BookStore.Identity.Common;

internal sealed class OriginAndCsrfEndpointFilter(IOptions<AuthenticationOptions> options) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var request = context.HttpContext.Request;
        var origin = request.Headers.Origin;
        if (!request.IsHttps || origin.Count != 1 ||
            !options.Value.AllowedOrigins.Contains(origin.ToString(), StringComparer.OrdinalIgnoreCase) ||
            request.Headers["X-BookStore-Request"].Count != 1 ||
            request.Headers["X-BookStore-Request"].ToString() != "1")
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        context.HttpContext.Response.Headers.CacheControl = "no-store";

        return await next(context);
    }
}
