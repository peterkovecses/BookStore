namespace BookStore.Identity.Features.Authentication;

internal static class AuthenticationEndpoints
{
    internal static void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("")
            .RequireCors(AuthenticationEndpointSupport.CorsPolicy)
            .AddEndpointFilter(AuthenticationEndpointSupport.ValidateOriginAsync);
        LoginEndpoint.MapEndpoint(group);
        RefreshEndpoint.MapEndpoint(group);
        LogoutEndpoint.MapEndpoint(group);
    }
}
