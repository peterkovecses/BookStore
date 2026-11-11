namespace BookStore.Identity.Features.Refresh;

internal sealed class RefreshHandler(IdentitySessionService sessions)
{
    internal Task<AuthenticationResult> HandleAsync(string token, CancellationToken cancellationToken) =>
        sessions.RefreshAsync(token, cancellationToken);
}
