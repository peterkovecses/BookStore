namespace BookStore.Identity.Features.Logout;

internal static class LogoutEndpoint
{
    internal static void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("logout", HandleAsync)
            .WithName("IdentityLogout")
            .WithDescription("Revokes the current refresh-token session and clears its cookie.")
            .Produces(StatusCodes.Status204NoContent);

    private static async Task<IResult> HandleAsync(
        LogoutHandler handler,
        HttpContext http,
        IOptions<AuthenticationOptions> options,
        CancellationToken cancellationToken)
    {
        if (AuthenticationEndpointSupport.TryReadRefreshToken(http, out var token))
        {
            await handler.HandleAsync(token, cancellationToken);
        }

        AuthenticationEndpointSupport.ClearRefreshCookie(http, options.Value);

        return Results.NoContent();
    }
}
