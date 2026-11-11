namespace BookStore.Identity.Features.Login;

internal static class LoginEndpoint
{
    internal static void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("login", HandleAsync)
            .RequireRateLimiting(IdentityAuthenticationPolicies.LoginRateLimit)
            .WithName("IdentityLogin")
            .WithDescription("Authenticates a confirmed customer and issues an access token and refresh cookie.")
            .Produces<AccessTokenResponse>()
            .ProducesValidationProblem();

    private static async Task<IResult> HandleAsync(
        LoginRequest request,
        LoginHandler handler,
        HttpContext http,
        IOptions<AuthenticationOptions> options,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || request.Email.Length > 256 ||
            !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(request.Email) ||
            string.IsNullOrEmpty(request.Password) || request.Password.Length > 1024)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["Login"] = ["Invalid login input."] });
        }

        var result = await handler.HandleAsync(request, cancellationToken);

        return AuthenticationEndpoints.ToResponse(result, http, options.Value);
    }
}
