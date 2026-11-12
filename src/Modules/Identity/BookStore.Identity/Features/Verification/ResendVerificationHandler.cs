namespace BookStore.Identity.Features.Verification;

internal sealed class ResendVerificationHandler(
    IIdentityDbContext context,
    IDataProtectionProvider protection,
    TimeProvider clock)
    : IRequestHandler<ResendVerificationCommand, Result>
{
    public async Task<Result> HandleAsync(ResendVerificationCommand request, CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();
        var value = new VerificationResendRequested(
            protection.CreateProtector(VerificationResendRequested.ProtectionPurpose).Protect(request.Email));
        context.OutboxMessages.Add(new OutboxMessage
        {
            Id = id,
            EventType = VerificationResendRequested.EventType,
            Payload = JsonSerializer.Serialize(value),
            CorrelationId = id.ToString(),
            CreatedAt = clock.GetUtcNow()
        });
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
