namespace BookStore.Identity.Contracts;

public sealed record AccessTokenResponse(string AccessToken, DateTimeOffset ExpiresAt);
