namespace BookStore.Identity.Common.Extensions;

internal static class IdentityRefreshCookieExtensions
{
    private const string CookieName = "__Secure-bookstore-refresh";
    private const string CookiePath = "/api/identity";

    internal static bool TryGetRefreshToken(this HttpRequest request, out string token)
    {
        token = request.Cookies[CookieName] ?? string.Empty;

        return token.Length == 43 && token.All(character =>
            char.IsAsciiLetterOrDigit(character) || character is '-' or '_');
    }

    internal static void SetRefreshToken(this HttpResponse response, string token, AuthenticationOptions options)
    {
        var cookie = CreateCookieOptions(options);
        cookie.MaxAge = options.RefreshTokenLifetime;
        response.Cookies.Append(CookieName, token, cookie);
    }

    internal static void ClearRefreshToken(this HttpResponse response, AuthenticationOptions options) =>
        response.Cookies.Delete(CookieName, CreateCookieOptions(options));

    private static CookieOptions CreateCookieOptions(AuthenticationOptions options) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = Enum.Parse<SameSiteMode>(options.CookieSameSite),
        Path = CookiePath,
        IsEssential = true
    };
}
