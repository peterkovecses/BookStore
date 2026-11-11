namespace BookStore.Identity.Features.Login;

internal sealed class LoginHandler(IdentitySessionService sessions)
{
    internal Task<AuthenticationResult> HandleAsync(LoginRequest request, CancellationToken cancellationToken) =>
        sessions.LoginAsync(request.Email, request.Password, cancellationToken);
}
