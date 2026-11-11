namespace BookStore.Identity.Features.Logout;

internal sealed class LogoutHandler(IdentitySessionService sessions)
{
    internal Task HandleAsync(string token, CancellationToken cancellationToken) =>
        sessions.LogoutAsync(token, cancellationToken);
}
