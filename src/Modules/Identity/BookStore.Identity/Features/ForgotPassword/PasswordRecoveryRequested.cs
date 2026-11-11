namespace BookStore.Identity.Features.ForgotPassword;

internal sealed record PasswordRecoveryRequested(string ProtectedEmail) : IEvent
{
    internal const string EventType = "identity.password-recovery-requested.v1";
    internal const string ProtectionPurpose = "BookStore.Identity.PasswordRecovery.v1";
}
