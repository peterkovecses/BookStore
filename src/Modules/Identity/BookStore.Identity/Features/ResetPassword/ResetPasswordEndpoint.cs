namespace BookStore.Identity.Features.ResetPassword;

internal static class ResetPasswordEndpoint
{
    internal static void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("password/reset", HandleAsync)
            .WithName("ResetPassword")
            .WithDescription("Resets a password using a recovery token.")
            .RequireRateLimiting(PasswordRecoveryRateLimiting.Reset)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem();

    private static async Task<IResult> HandleAsync(
        ResetPasswordRequest request,
        IRequestHandler<ResetPasswordCommand, Result> handler,
        CancellationToken cancellationToken) =>
        await handler.HandleAsync(new ResetPasswordCommand(request.UserId, request.Token, request.NewPassword), cancellationToken)
            .ToMinimalApiResultAsync();
}
