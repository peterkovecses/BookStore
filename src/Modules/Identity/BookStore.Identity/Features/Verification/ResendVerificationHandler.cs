namespace BookStore.Identity.Features.Verification;

internal sealed class ResendVerificationHandler(
    UserManager<ApplicationUser> users,
    IVerificationRequestFactory verificationRequestFactory,
    IdentityDbContext context)
    : IRequestHandler<ResendVerificationCommand, Result>
{
    public async Task<Result> HandleAsync(ResendVerificationCommand request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await users.FindByEmailAsync(request.Email);
        if (user is null || await users.IsEmailConfirmedAsync(user))
        {
            return Result.Success();
        }

        cancellationToken.ThrowIfCancellationRequested();
        var message = await verificationRequestFactory.CreateAsync(user, cancellationToken);
        context.OutboxMessages.Add(message);
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
