namespace BookStore.Identity.Features.PasswordRecovery;

internal sealed record PasswordRecoveryRequested(string ProtectedEmail) : IEvent
{
    internal const string EventType = "identity.password-recovery-requested.v1";
    internal const string ProtectionPurpose = "BookStore.Identity.PasswordRecovery.v1";
}

internal sealed class PasswordRecoveryRequestedHandler(
    UserManager<ApplicationUser> users,
    IdentityDbContext context,
    IDataProtectionProvider protection,
    IOptions<PasswordRecoveryOptions> options,
    TimeProvider clock) : IEventHandler<PasswordRecoveryRequested>
{
    internal const string EmailEventType = "identity.password-reset-email-requested.v1";

    public async Task HandleAsync(PasswordRecoveryRequested value, CancellationToken cancellationToken)
    {
        var email = protection.CreateProtector(PasswordRecoveryRequested.ProtectionPurpose).Unprotect(value.ProtectedEmail);
        var user = await users.FindByEmailAsync(email);
        if (user is null)
        {
            return;
        }

        var now = clock.GetUtcNow();
        var token = await users.GeneratePasswordResetTokenAsync(user);
        var link = QueryHelpers.AddQueryString(options.Value.LinkBaseUrl, new Dictionary<string, string?>
        {
            ["userId"] = user.Id,
            ["token"] = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token))
        });
        var message = new PasswordResetEmailRequested(user.Id, user.Email!, user.FirstName,
            protection.CreateProtector("BookStore.Identity.PasswordResetLink.v1")
                .CreateProtector(user.Id).CreateProtector(user.Email!).Protect(link), now + options.Value.TokenLifetime)
        {
            IssuedAt = now
        };
        var id = Guid.NewGuid();
        context.OutboxMessages.Add(new OutboxMessage
        {
            Id = id,
            EventType = EmailEventType,
            Payload = JsonSerializer.Serialize(message),
            CorrelationId = id.ToString(),
            CreatedAt = now
        });
        await context.SaveChangesAsync(cancellationToken);
    }
}
