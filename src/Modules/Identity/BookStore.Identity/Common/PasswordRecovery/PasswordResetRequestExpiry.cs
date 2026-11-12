namespace BookStore.Identity.Common.PasswordRecovery;

internal sealed class PasswordResetRequestExpiry(IOptions<PasswordRecoveryOptions> options)
    : IOutboxExpiry<PasswordResetEmailRequested>
{
    public bool IsExpired(PasswordResetEmailRequested value, DateTimeOffset now) =>
        now >= value.ExpiresAt || (now >= value.IssuedAt && now - value.IssuedAt >= options.Value.TokenLifetime);
}
