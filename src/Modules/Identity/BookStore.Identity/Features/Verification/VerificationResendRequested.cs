namespace BookStore.Identity.Features.Verification;

internal sealed record VerificationResendRequested(string ProtectedEmail) : IEvent
{
    internal const string EventType = "identity.verification-resend-requested.v1";
    internal const string ProtectionPurpose = "BookStore.Identity.VerificationResend.v1";
}

internal sealed class VerificationResendRequestedHandler(
    UserManager<ApplicationUser> users,
    IVerificationRequestFactory verificationRequestFactory,
    IIdentityDbContext context,
    IDataProtectionProvider protection) : IEventHandler<VerificationResendRequested>
{
    public async Task HandleAsync(VerificationResendRequested value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var email = protection.CreateProtector(VerificationResendRequested.ProtectionPurpose).Unprotect(value.ProtectedEmail);
        var user = await users.FindByEmailAsync(email);
        if (user is null || await users.IsEmailConfirmedAsync(user))
        {
            return;
        }

        var message = await verificationRequestFactory.CreateAsync(user, cancellationToken);
        context.OutboxMessages.Add(message);
        await context.SaveChangesAsync(cancellationToken);
    }
}
