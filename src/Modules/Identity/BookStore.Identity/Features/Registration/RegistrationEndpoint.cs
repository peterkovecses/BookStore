namespace BookStore.Identity.Features.Registration;

internal static class RegistrationEndpoint
{
    internal static void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost("registration", HandleAsync)
            .WithName("RegisterCustomer")
            .WithDescription("Registers an unconfirmed customer account.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem();

    private static async Task<IResult> HandleAsync(
        RegistrationRequest request,
        IRequestHandler<RegisterCustomer, Result> handler,
        CancellationToken cancellationToken) =>
        await handler.HandleAsync(new RegisterCustomer(request), cancellationToken).ToMinimalApiResultAsync();
}
