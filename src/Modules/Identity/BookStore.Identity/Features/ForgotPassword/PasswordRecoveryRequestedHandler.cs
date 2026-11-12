namespace BookStore.Identity.Features.ForgotPassword;

internal sealed class PasswordRecoveryRequestedHandler(
    UserManager<ApplicationUser> users,
    IdentityDbContext context,
    IDataProtectionProvider protection,
    OutboxDispatchContext dispatchContext,
    IOptions<PasswordRecoveryOptions> options,
    TimeProvider clock) : IEventHandler<PasswordRecoveryRequested>
{
    internal const string EmailEventType = "identity.password-reset-email-requested.v1";

    public async Task HandleAsync(PasswordRecoveryRequested value, CancellationToken cancellationToken) =>
        await context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
            await AcquireRequestLockAsync(dispatchContext.EventId, cancellationToken);
            if (await context.PasswordRecoveryRequestReceipts.AnyAsync(
                receipt => receipt.RequestEventId == dispatchContext.EventId, cancellationToken))
            {
                await transaction.CommitAsync(cancellationToken);

                return;
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
                await context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                return;
            }

            var now = clock.GetUtcNow();
            var token = await users.GeneratePasswordResetTokenAsync(user);
            var link = QueryHelpers.AddQueryString(options.Value.LinkBaseUrl, new Dictionary<string, string?>
            {
                ["userId"] = user.Id,
                ["token"] = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token))
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
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });

    private async Task AcquireRequestLockAsync(Guid eventId, CancellationToken cancellationToken)
    {
        var resource = $"identity.password-recovery:{eventId:N}";
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.Transaction = context.Database.CurrentTransaction!.GetDbTransaction();
        command.CommandText = """
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock @Resource = @resource, @LockMode = 'Exclusive',
                @LockOwner = 'Transaction', @LockTimeout = 10000;
            SELECT @result;
            """;
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@resource";
        parameter.Value = resource;
        command.Parameters.Add(parameter);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        if (result is not int lockResult || lockResult < 0)
        {
            throw new TimeoutException("Unable to acquire the password recovery request lock.");
        }
    }
}
