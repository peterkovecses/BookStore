namespace BookStore.Identity.Features.Verification;

internal static class VerificationEndpoint
{
    internal static IEndpointRouteBuilder MapVerificationEndpoints(this IEndpointRouteBuilder app)
    {
        MapConfirmationEndpoint(app);
        MapResendEndpoint(app);

        return app;
    }

    internal static void MapConfirmationEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("verification/confirmation", HandleConfirmationAsync)
            .WithName("ConfirmEmail")
            .WithDescription("Confirms a customer's email address.")
            .RequireRateLimiting(VerificationRateLimitPolicies.Confirmation)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem();

    internal static void MapResendEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("verification/resend", HandleResendAsync)
            .WithName("ResendEmailVerification")
            .WithDescription("Requests email verification without disclosing account status.")
            .RequireRateLimiting(VerificationRateLimitPolicies.Resend)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem();

    private static async Task<IResult> HandleConfirmationAsync(
        ConfirmationRequest request,
        IRequestHandler<ConfirmEmailCommand, Result> handler,
        CancellationToken cancellationToken) =>
        await handler.HandleAsync(new ConfirmEmailCommand(request), cancellationToken).ToMinimalApiResultAsync();

    private static async Task<IResult> HandleResendAsync(
        ResendVerificationRequest request,
        IRequestHandler<ResendVerificationCommand, Result> handler,
        CancellationToken cancellationToken) =>
        await handler.HandleAsync(new ResendVerificationCommand(request), cancellationToken).ToMinimalApiResultAsync();
}
