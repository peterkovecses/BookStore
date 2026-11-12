namespace BookStore.Identity.Features.ForgotPassword;

internal sealed class PasswordRecoveryRequestedExpiry(IOptions<PasswordRecoveryOptions> options)
    : IOutboxExpiry<PasswordRecoveryRequested>
{
    public bool IsExpired(PasswordRecoveryRequested value, DateTimeOffset now) =>
        value.ExpiresAt is { } expiresAt && now >= expiresAt;

    public bool IsExpired(PasswordRecoveryRequested value, DateTimeOffset now, DateTimeOffset createdAt)
    {
        var configuredExpiry = createdAt + options.Value.TokenLifetime;
        var expiresAt = value.ExpiresAt is { } recordedExpiry && recordedExpiry < configuredExpiry
            ? recordedExpiry
            : configuredExpiry;

        return now >= expiresAt;
    }
}
