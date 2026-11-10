namespace BookStore.Identity.Contracts;

public sealed class RegistrationRequest
{
    public string? Email { get; init; }
    public string? Password { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
}
