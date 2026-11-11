namespace BookStore.Identity.Infrastructure;

internal sealed class RefreshToken
{
    public required string Hash { get; set; }
    public Guid FamilyId { get; set; }
    public required string UserId { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public bool Rotated { get; set; }
    public bool Revoked { get; set; }
}
