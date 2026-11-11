namespace BookStore.Identity.Features.Refresh;

internal static class RefreshEndpoint
{
    internal static void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("refresh", HandleAsync)
            .WithName("IdentityRefresh")
            .WithDescription("Rotates the current refresh token and issues a new access token.")
            .Produces<AccessTokenResponse>()
            .Produces(StatusCodes.Status401Unauthorized);

    private static async Task<IResult> HandleAsync(
        RefreshHandler handler,
        HttpContext http,
        IOptions<AuthenticationOptions> options,
        CancellationToken cancellationToken)
    {
        if (!http.Request.TryGetRefreshToken(out var token))
        {
            return Results.Unauthorized();
        }

        var result = await handler.HandleAsync(token, cancellationToken);

        return AuthenticationEndpoints.ToResponse(result, http, options.Value);
    }
}
