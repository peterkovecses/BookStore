namespace BookStore.Identity.Features.ForgotPassword;

internal static class ForgotPasswordEndpoint
{
    internal static void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("password/forgot", HandleAsync)
            .WithName("ForgotPassword")
            .WithDescription("Requests password recovery without disclosing account status.")
            .RequireRateLimiting(PasswordRecoveryRateLimiting.Forgot)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem();

    private static async Task<IResult> HandleAsync(
        ForgotPasswordRequest request,
        IRequestHandler<ForgotPasswordCommand, Result> handler,
        CancellationToken cancellationToken) =>
        await handler.HandleAsync(new ForgotPasswordCommand(request.Email), cancellationToken).ToMinimalApiResultAsync();
}
