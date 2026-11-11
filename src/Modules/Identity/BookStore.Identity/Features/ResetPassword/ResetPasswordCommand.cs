namespace BookStore.Identity.Features.ResetPassword;

internal sealed record ResetPasswordCommand(string UserId, string Token, string NewPassword) : IRequest<Result>;
