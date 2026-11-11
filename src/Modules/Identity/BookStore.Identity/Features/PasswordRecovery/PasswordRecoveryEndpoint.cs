namespace BookStore.Identity.Features.PasswordRecovery;

internal static class PasswordRecoveryEndpoint
{
    internal static void MapEndpoints(IEndpointRouteBuilder app)
    {
        app.MapPost("password/forgot", ForgotAsync)
            .WithName("ForgotPassword")
            .WithDescription("Requests password recovery without disclosing account status.")
            .RequireRateLimiting(PasswordRecoveryRateLimiting.Forgot)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem();
        app.MapPost("password/reset", ResetAsync)
            .WithName("ResetPassword")
            .WithDescription("Resets a password using a recovery token.")
            .RequireRateLimiting(PasswordRecoveryRateLimiting.Reset)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem();
    }

    private static async Task<IResult> ForgotAsync(ForgotPasswordRequest request,
        IRequestHandler<ForgotPasswordCommand, Result> handler, CancellationToken cancellationToken) =>
        await handler.HandleAsync(new ForgotPasswordCommand(request), cancellationToken).ToMinimalApiResultAsync();

    private static async Task<IResult> ResetAsync(ResetPasswordRequest request,
        IRequestHandler<ResetPasswordCommand, Result> handler, CancellationToken cancellationToken) =>
        await handler.HandleAsync(new ResetPasswordCommand(request), cancellationToken).ToMinimalApiResultAsync();
}

internal sealed class ForgotPasswordCommand(ForgotPasswordRequest request) : IRequest<Result>
{
    public string Email { get; } = request.Email;
}

internal sealed class ResetPasswordCommand(ResetPasswordRequest request) : IRequest<Result>
{
    public string UserId { get; } = request.UserId;
    public string Token { get; } = request.Token;
    public string NewPassword { get; } = request.NewPassword;
}

internal sealed class ForgotPasswordValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordValidator() => RuleFor(request => request.Email).NotEmpty().EmailAddress().MaximumLength(256);
}

internal sealed class ResetPasswordValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordValidator()
    {
        RuleFor(request => request.UserId).NotEmpty().MaximumLength(256);
        RuleFor(request => request.Token).NotEmpty().MaximumLength(2048);
        RuleFor(request => request.NewPassword).NotEmpty().MaximumLength(1024);
    }
}
