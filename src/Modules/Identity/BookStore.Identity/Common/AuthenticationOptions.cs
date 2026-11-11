namespace BookStore.Identity.Common;

internal sealed class AuthenticationOptions
{
    public string Audience { get; init; } = string.Empty;
    public string Issuer { get; init; } = string.Empty;
    public string SigningKey { get; init; } = string.Empty;
    public string[] AllowedOrigins { get; init; } = [];
    public TimeSpan AccessTokenLifetime { get; init; } = TimeSpan.FromMinutes(10);
    public TimeSpan RefreshTokenLifetime { get; init; } = TimeSpan.FromDays(14);
    public string CookieSameSite { get; init; } = "Strict";
    public int LoginPermitLimit { get; init; } = 20;
    public int LoginWindowSeconds { get; init; } = 60;
    public int LockoutAttempts { get; init; } = 5;
    public TimeSpan LockoutDuration { get; init; } = TimeSpan.FromMinutes(15);

    public bool IsValid()
    {
        if (string.IsNullOrWhiteSpace(SigningKey) || AllowedOrigins is null)
        {
            return false;
        }

        byte[] key;
        try
        {
            key = Convert.FromBase64String(SigningKey);
        }
        catch (FormatException)
        {
            return false;
        }

        return key.Length >= 32 &&
            !string.IsNullOrWhiteSpace(Issuer) && !string.IsNullOrWhiteSpace(Audience) &&
            AccessTokenLifetime >= TimeSpan.FromMinutes(1) && AccessTokenLifetime <= TimeSpan.FromMinutes(15) &&
            RefreshTokenLifetime >= AccessTokenLifetime && RefreshTokenLifetime <= TimeSpan.FromDays(30) &&
            CookieSameSite is "Strict" or "Lax" or "None" &&
            LoginPermitLimit > 0 && LoginWindowSeconds > 0 &&
            LockoutAttempts is >= 1 and <= 100 && LockoutDuration >= TimeSpan.FromMinutes(1) &&
            LockoutDuration <= TimeSpan.FromDays(1) &&
            AllowedOrigins.Length > 0 && AllowedOrigins.All(IsHttpsOrigin);
    }

    private static bool IsHttpsOrigin(string origin) =>
        Uri.TryCreate(origin, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo) &&
        uri.AbsolutePath == "/" && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment) &&
        string.Equals(origin, uri.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase);
}
