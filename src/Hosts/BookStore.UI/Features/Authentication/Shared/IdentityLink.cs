namespace BookStore.UI.Features.Authentication;

internal sealed record IdentityLink(string UserId, string Token);

internal static class IdentityLinkValidator
{
    internal static bool IsValid(IdentityLink? link) =>
        link is not null &&
        !string.IsNullOrWhiteSpace(link.UserId) && link.UserId.Length <= 256 &&
        !string.IsNullOrWhiteSpace(link.Token) && link.Token.Length <= 2048 &&
        link.Token.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_');
}
