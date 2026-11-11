using Kovecses.Requests;

namespace BookStore.Identity.Contracts;

public sealed record VerificationEmailRequested(
    string UserId,
    string Email,
    string FirstName,
    string ProtectedLink,
    DateTimeOffset ExpiresAt) : IEvent
{
    public DateTimeOffset IssuedAt { get; init; }
}
