namespace BookStore.Identity.Features.Registration;

internal sealed class RegisterCustomer(RegistrationRequest request) : IRequest<Result>
{
    public string Email { get; } = request.Email ?? string.Empty;
    public string Password { get; } = request.Password ?? string.Empty;
    public string FirstName { get; } = request.FirstName ?? string.Empty;
    public string LastName { get; } = request.LastName ?? string.Empty;
}
