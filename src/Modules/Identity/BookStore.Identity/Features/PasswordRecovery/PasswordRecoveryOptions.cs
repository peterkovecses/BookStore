namespace BookStore.Identity.Features.PasswordRecovery;

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

internal sealed class PasswordResetTokenProviderOptions : DataProtectionTokenProviderOptions;

internal sealed class PasswordResetTokenProvider(
    IDataProtectionProvider protection,
    IOptions<PasswordResetTokenProviderOptions> options,
    ILogger<DataProtectorTokenProvider<ApplicationUser>> logger)
    : DataProtectorTokenProvider<ApplicationUser>(protection, options, logger);

internal sealed class PasswordResetRequestExpiry(IOptions<PasswordRecoveryOptions> options)
    : IOutboxExpiry<PasswordResetEmailRequested>
{
    public bool IsExpired(PasswordResetEmailRequested value, DateTimeOffset now) =>
        now >= value.ExpiresAt || (now >= value.IssuedAt && now - value.IssuedAt >= options.Value.TokenLifetime);
}
