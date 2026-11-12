namespace BookStore.Identity.Features.ForgotPassword;

internal static class ForgotPasswordEndpoint
{
    internal static void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("password/forgot", HandleAsync)
            .WithName("ForgotPassword")
            .WithDescription("Requests password recovery without disclosing account status.")
            .RequireRateLimiting(PasswordRecoveryRateLimiting.Forgot)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status429TooManyRequests)
            .ProducesValidationProblem();

    private static async Task<IResult> HandleAsync(
        ForgotPasswordRequest request,
        IRequestHandler<ForgotPasswordCommand, Result<EmailRequestOutcome>> handler,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new ForgotPasswordCommand(request.Email), cancellationToken);
        if (result.IsFailure)
        {
            return await Task.FromResult(result).ToMinimalApiResultAsync();
        }

        var outcome = result.Data!;
        context.Response.Headers.RetryAfter = outcome.RetryAfterSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture);

        return outcome.Accepted ? Results.NoContent() : Results.StatusCode(StatusCodes.Status429TooManyRequests);
    }
}
