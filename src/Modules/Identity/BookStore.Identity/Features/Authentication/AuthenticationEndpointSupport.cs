namespace BookStore.Identity.Features.Authentication;

internal static class AuthenticationEndpointSupport
{
    private const string CookieName = "__Secure-bookstore-refresh";
    private const string CookiePath = "/api/identity";
    internal const string CorsPolicy = "identity-authentication";
    internal const string LoginPolicy = "identity-login";

    internal static async ValueTask<object?> ValidateOriginAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
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

    internal static bool TryReadRefreshToken(HttpContext http, out string token)
    {
        token = http.Request.Cookies[CookieName] ?? string.Empty;

        return token.Length == 43 && token.All(character =>
            char.IsAsciiLetterOrDigit(character) || character is '-' or '_');
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

        var cookie = CreateCookieOptions(options);
        cookie.MaxAge = options.RefreshTokenLifetime;
        http.Response.Cookies.Append(CookieName, result.RefreshToken, cookie);

        return Results.Ok(result.AccessToken);
    }

    internal static void ClearRefreshCookie(HttpContext http, AuthenticationOptions options) =>
        http.Response.Cookies.Delete(CookieName, CreateCookieOptions(options));

    private static CookieOptions CreateCookieOptions(AuthenticationOptions options) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = Enum.Parse<SameSiteMode>(options.CookieSameSite),
        Path = CookiePath,
        IsEssential = true
    };
}
