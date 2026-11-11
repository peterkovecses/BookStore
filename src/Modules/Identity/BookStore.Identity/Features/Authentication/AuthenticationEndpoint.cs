namespace BookStore.Identity.Features.Authentication;

internal static class AuthenticationEndpoint
{
    private const string CookieName = "__Secure-bookstore-refresh";
    private const string CookiePath = "/api/identity";
    internal const string CorsPolicy = "identity-authentication";
    internal const string LoginPolicy = "identity-login";

    internal static void MapEndpoints(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("").RequireCors(CorsPolicy).AddEndpointFilter(ValidateOriginAsync);
        group.MapPost("login", LoginAsync).RequireRateLimiting(LoginPolicy);
        group.MapPost("refresh", RefreshAsync);
        group.MapPost("logout", LogoutAsync);
    }

    private static async ValueTask<object?> ValidateOriginAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var request = context.HttpContext.Request;
        var settings = context.HttpContext.RequestServices.GetRequiredService<IOptions<AuthenticationOptions>>().Value;
        var origin = request.Headers.Origin;
        if (!request.IsHttps || origin.Count != 1 ||
            !settings.AllowedOrigins.Contains(origin.ToString(), StringComparer.OrdinalIgnoreCase) ||
            request.Headers["X-BookStore-Request"].Count != 1 ||
            request.Headers["X-BookStore-Request"].ToString() != "1")
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        context.HttpContext.Response.Headers.CacheControl = "no-store";

        return await next(context);
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request, AuthenticationService service, HttpContext http,
        IOptions<AuthenticationOptions> options, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || request.Email.Length > 256 ||
            !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(request.Email) ||
            string.IsNullOrEmpty(request.Password) || request.Password.Length > 1024)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["Login"] = ["Invalid login input."] });
        }

        var result = await service.LoginAsync(request.Email, request.Password, cancellationToken);

        return ToResponse(result, http, options.Value);
    }

    private static async Task<IResult> RefreshAsync(
        AuthenticationService service, HttpContext http, IOptions<AuthenticationOptions> options,
        CancellationToken cancellationToken)
    {
        if (!TryReadToken(http, out var token))
        {
            return Results.Unauthorized();
        }

        var result = await service.RefreshAsync(token, cancellationToken);

        return ToResponse(result, http, options.Value);
    }

    private static async Task<IResult> LogoutAsync(
        AuthenticationService service, HttpContext http, IOptions<AuthenticationOptions> options,
        CancellationToken cancellationToken)
    {
        if (TryReadToken(http, out var token))
        {
            await service.LogoutAsync(token, cancellationToken);
        }

        http.Response.Cookies.Delete(CookieName, CookieOptions(options.Value));

        return Results.NoContent();
    }

    private static IResult ToResponse(AuthenticationResult result, HttpContext http, AuthenticationOptions options)
    {
        if (result.AccessToken is null || result.RefreshToken is null)
        {
            if (result.EmailUnconfirmed)
            {
                return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Email address has not been confirmed.");
            }

            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid credentials or session.");
        }

        var cookie = CookieOptions(options);
        cookie.MaxAge = options.RefreshTokenLifetime;
        http.Response.Cookies.Append(CookieName, result.RefreshToken, cookie);

        return Results.Ok(result.AccessToken);
    }

    private static bool TryReadToken(HttpContext http, out string token)
    {
        token = http.Request.Cookies[CookieName] ?? string.Empty;

        return token.Length == 43 && token.All(character =>
            char.IsAsciiLetterOrDigit(character) || character is '-' or '_');
    }

    private static CookieOptions CookieOptions(AuthenticationOptions options) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = Enum.Parse<SameSiteMode>(options.CookieSameSite),
        Path = CookiePath,
        IsEssential = true
    };
}
