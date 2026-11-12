namespace BookStore.Identity.Contracts;

public sealed class ForgotPasswordRequest
{
    public required string Email { get; init; }
}
