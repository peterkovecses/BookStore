namespace BookStore.Identity.Features.Verification;

internal sealed class ResendVerificationHandler(
    IVerificationResendStore store,
    IDataProtectionProvider protection,
    TimeProvider clock)
    : IRequestHandler<ResendVerificationCommand, Result<ResendVerificationOutcome>>
{
    public async Task<Result<ResendVerificationOutcome>> HandleAsync(ResendVerificationCommand request, CancellationToken cancellationToken)
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
        var outcome = await store.EnqueueAsync(request.Email, message, cancellationToken);

        return Result.Success(outcome);
    }
}
