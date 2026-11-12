namespace BookStore.Identity.Features.ForgotPassword;

internal sealed class PasswordRecoveryRequestedHandler(
    UserManager<ApplicationUser> users,
    IIdentityDbContext context,
    IDataProtectionProvider protection,
    OutboxDispatchContext dispatchContext,
    IOptions<PasswordRecoveryOptions> options,
    TimeProvider clock) : IEventHandler<PasswordRecoveryRequested>
{
    internal const string EmailEventType = "identity.password-reset-email-requested.v1";

    public async Task HandleAsync(PasswordRecoveryRequested value, CancellationToken cancellationToken) =>
        await context.ExecuteInTransactionAsync(async (transaction, token) =>
        {
            var resource = $"identity.password-recovery:{dispatchContext.EventId:N}";
            var lockResult = await transaction.TryAcquireLockAsync(resource, 10_000, token);
            if (lockResult is not int result || result < 0)
            {
                throw new TimeoutException("Unable to acquire the password recovery request lock.");
            }

            if (await context.PasswordRecoveryRequestReceipts.AnyAsync(
                receipt => receipt.RequestEventId == dispatchContext.EventId, token))
            {
                await transaction.CommitAsync(token);

                return true;
            }

            var email = protection.CreateProtector(PasswordRecoveryRequested.ProtectionPurpose).Unprotect(value.ProtectedEmail);
            var user = await users.FindByEmailAsync(email);
            if (user is null)
            {
                context.PasswordRecoveryRequestReceipts.Add(new()
                {
                    RequestEventId = dispatchContext.EventId,
                    ProcessedAt = clock.GetUtcNow()
                });
                await context.SaveChangesAsync(token);
                await transaction.CommitAsync(token);

                return true;
            }

            var now = clock.GetUtcNow();
            var resetToken = await users.GeneratePasswordResetTokenAsync(user);
            var link = QueryHelpers.AddQueryString(options.Value.LinkBaseUrl, new Dictionary<string, string?>
            {
                ["userId"] = user.Id,
                ["token"] = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(resetToken))
            });
            var id = Guid.NewGuid();
            var message = new PasswordResetEmailRequested(user.Id, user.Email!, user.FirstName,
                protection.CreateProtector("BookStore.Identity.PasswordResetLink.v2")
                    .CreateProtector(user.Id).Protect(link), now + options.Value.TokenLifetime)
            {
                IssuedAt = now
            };
            context.PasswordRecoveryRequestReceipts.Add(new()
            {
                RequestEventId = dispatchContext.EventId,
                EmailEventId = id,
                ProcessedAt = now
            });
            context.OutboxMessages.Add(new OutboxMessage
            {
                Id = id,
                EventType = EmailEventType,
                Payload = JsonSerializer.Serialize(message),
                CorrelationId = dispatchContext.CorrelationId,
                CreatedAt = now
            });
            await context.SaveChangesAsync(token);
            await transaction.CommitAsync(token);
            return true;
        }, cancellationToken);
}
