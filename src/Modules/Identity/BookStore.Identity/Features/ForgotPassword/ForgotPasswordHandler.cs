namespace BookStore.Identity.Features.ForgotPassword;

internal sealed class ForgotPasswordHandler(IEmailRequestCooldownStore store, IDataProtectionProvider protection,
    IOptions<PasswordRecoveryOptions> options, TimeProvider clock)
    : IRequestHandler<ForgotPasswordCommand, Result<EmailRequestOutcome>>
{
    public async Task<Result<EmailRequestOutcome>> HandleAsync(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();
        var now = clock.GetUtcNow();
        var value = new PasswordRecoveryRequested(
            protection.CreateProtector(PasswordRecoveryRequested.ProtectionPurpose).Protect(request.Email),
            now + options.Value.TokenLifetime);
        var message = new OutboxMessage
        {
            Id = id,
            EventType = PasswordRecoveryRequested.EventType,
            Payload = JsonSerializer.Serialize(value),
            CorrelationId = id.ToString(),
            CreatedAt = now
        };
        var outcome = await store.EnqueueAsync(EmailRequestPurpose.PasswordRecovery, request.Email, message, cancellationToken);

        return Result.Success(outcome);
    }
}
