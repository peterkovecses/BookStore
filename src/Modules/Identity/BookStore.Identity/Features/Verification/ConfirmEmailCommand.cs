namespace BookStore.Identity.Features.Verification;

internal sealed class ConfirmEmailCommand(ConfirmationRequest request) : IRequest<Result>
{
    public string UserId { get; } = request.UserId;
    public string EncodedToken { get; } = request.Token;
}

public sealed class ConfirmationRequest
{
    public string UserId { get; init; } = string.Empty;
    public string Token { get; init; } = string.Empty;
}
