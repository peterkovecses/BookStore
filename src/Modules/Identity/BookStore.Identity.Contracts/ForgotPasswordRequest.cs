namespace BookStore.Identity.Contracts;

public sealed class ForgotPasswordRequest
{
    public string Email { get; init; } = string.Empty;
}
