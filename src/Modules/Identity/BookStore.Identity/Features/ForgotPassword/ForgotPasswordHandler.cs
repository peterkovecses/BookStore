namespace BookStore.Identity.Features.ForgotPassword;

/// <summary>
/// Handles password recovery requests by applying the email cooldown and enqueueing a protected recovery event for later processing.
/// </summary>
internal sealed class ForgotPasswordHandler(IEmailRequestCooldownStore store, IDataProtectionProvider protection,
    IOptions<PasswordRecoveryOptions> options, TimeProvider clock)
    : IRequestHandler<ForgotPasswordCommand, Result<EmailRequestOutcome>>
{
    public async Task<Result<EmailRequestOutcome>> HandleAsync(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();
        var now = clock.GetUtcNow();
        var @event = CreateEvent(request, now);
        var message = CreateOutboxMessage(id, now, @event);
        var result = await store.EnqueueAsync(EmailRequestPurpose.PasswordRecovery, request.Email, message, cancellationToken);

        return Result.Success(result);
    }

    private static OutboxMessage CreateOutboxMessage(Guid id, DateTimeOffset now, PasswordRecoveryRequested @event) => new()
    {
        Id = id,
        EventType = PasswordRecoveryRequested.EventType,
        Payload = JsonSerializer.Serialize(@event),
        CorrelationId = id.ToString(),
        CreatedAt = now
    };

    private PasswordRecoveryRequested CreateEvent(ForgotPasswordCommand request, DateTimeOffset now) => new(
            protection.CreateProtector(PasswordRecoveryRequested.ProtectionPurpose).Protect(request.Email),
            now + options.Value.TokenLifetime);
}
