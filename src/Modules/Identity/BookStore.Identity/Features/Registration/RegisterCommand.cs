namespace BookStore.Identity.Features.Registration;

internal sealed class RegisterCommand(RegistrationRequest request) : IRequest<Result>
{
    public string Email { get; } = request.Email;
    public string Password { get; } = request.Password;
    public string FirstName { get; } = request.FirstName;
    public string LastName { get; } = request.LastName;
}
