namespace BookStore.Identity.Contracts;

public sealed class ResetPasswordRequest
{
    public required string UserId { get; init; }
    public required string Token { get; init; }
    public required string NewPassword { get; init; }
}
