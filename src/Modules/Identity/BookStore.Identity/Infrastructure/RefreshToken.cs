namespace BookStore.Identity.Infrastructure;

internal sealed class RefreshToken
{
    public required string Hash { get; init; }
    public Guid FamilyId { get; init; }
    public required string UserId { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public bool Rotated { get; set; }
    public bool Revoked { get; set; }
}
