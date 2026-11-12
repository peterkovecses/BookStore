namespace BookStore.Identity.Infrastructure;

internal sealed class VerificationResendCooldown
{
    public string EmailHash { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
}
