namespace BookStore.Identity.Common.Extensions;

internal static class AuthenticationEndpoints
{
    internal static void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("")
            .RequireCors(IdentityAuthenticationPolicies.Cors)
            .AddEndpointFilter<OriginAndCsrfEndpointFilter>();
        LoginEndpoint.MapEndpoint(group);
        RefreshEndpoint.MapEndpoint(group);
        LogoutEndpoint.MapEndpoint(group);
    }

    internal static IResult ToResponse(AuthenticationResult result, HttpContext http, AuthenticationOptions options)
    {
        if (result.AccessToken is null || result.RefreshToken is null)
        {
            if (result.EmailUnconfirmed)
            {
                return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Email address has not been confirmed.");
            }

            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid credentials or session.");
        }

        http.Response.SetRefreshToken(result.RefreshToken, options);

        return Results.Ok(result.AccessToken);
    }
}
