namespace BookStore.Identity.Features.Verification;

internal sealed class ResendVerificationCommand(ResendVerificationRequest request) : IRequest<Result<EmailRequestOutcome>>
{
    public string Email { get; } = request.Email;
}

public sealed class ResendVerificationRequest
{
    public string Email { get; init; } = string.Empty;
}
