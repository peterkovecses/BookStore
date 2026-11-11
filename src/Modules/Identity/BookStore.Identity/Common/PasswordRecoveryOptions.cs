namespace BookStore.Identity.Common;

internal sealed class PasswordRecoveryOptions
{
    public string LinkBaseUrl { get; init; } = string.Empty;
    public TimeSpan TokenLifetime { get; init; } = TimeSpan.FromHours(1);

    public bool IsValid() =>
        TokenLifetime > TimeSpan.Zero &&
        Uri.TryCreate(LinkBaseUrl, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps &&
        string.IsNullOrEmpty(uri.UserInfo) &&
        string.IsNullOrEmpty(uri.Query) &&
        string.IsNullOrEmpty(uri.Fragment);
}
