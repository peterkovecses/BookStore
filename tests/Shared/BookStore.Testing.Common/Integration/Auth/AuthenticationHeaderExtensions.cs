namespace BookStore.Testing.Common.Integration.Auth;

public static class AuthenticationHeaderExtensions
{
    public static void SetAuthorizationHeader(this HttpRequestHeaders headers, string token)
    {
        headers.Authorization = new AuthenticationHeaderValue(JwtBearerDefaults.AuthenticationScheme, token);
    }
}