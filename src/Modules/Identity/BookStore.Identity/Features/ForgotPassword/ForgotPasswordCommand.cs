namespace BookStore.Identity.Features.ForgotPassword;

internal sealed record ForgotPasswordCommand(string Email) : IRequest<Result>;
