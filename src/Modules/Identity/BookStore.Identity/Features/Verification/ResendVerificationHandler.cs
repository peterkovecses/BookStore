namespace BookStore.Identity.Features.Verification;

internal sealed class ResendVerificationHandler(
    IEmailRequestCooldownStore store,
    IDataProtectionProvider protection,
    TimeProvider clock)
    : IRequestHandler<ResendVerificationCommand, Result<EmailRequestOutcome>>
{
    public async Task<Result<EmailRequestOutcome>> HandleAsync(ResendVerificationCommand request, CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();
        var value = new VerificationResendRequested(
            protection.CreateProtector(VerificationResendRequested.ProtectionPurpose).Protect(request.Email));
        var message = new OutboxMessage
        {
            Id = id,
            EventType = VerificationResendRequested.EventType,
            Payload = JsonSerializer.Serialize(value),
            CorrelationId = id.ToString(),
            CreatedAt = clock.GetUtcNow()
        };
        var outcome = await store.EnqueueAsync(EmailRequestPurpose.Verification, request.Email, message, cancellationToken);

        return Result.Success(outcome);
    }
}
