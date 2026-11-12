namespace BookStore.Identity.Features.Verification;

internal sealed class ResendVerificationCommand(ResendVerificationRequest request) : IRequest<Result<ResendVerificationOutcome>>
{
    public string Email { get; } = request.Email;
}

internal sealed record ResendVerificationOutcome(bool Accepted, int RetryAfterSeconds);

public sealed class ResendVerificationRequest
{
    public string Email { get; init; } = string.Empty;
}
