namespace BookStore.Identity.Features.PasswordRecovery;

internal sealed class ForgotPasswordHandler(IdentityDbContext context, IDataProtectionProvider protection, TimeProvider clock)
    : IRequestHandler<ForgotPasswordCommand, Result>
{
    public async Task<Result> HandleAsync(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();
        var value = new PasswordRecoveryRequested(
            protection.CreateProtector(PasswordRecoveryRequested.ProtectionPurpose).Protect(request.Email));
        context.OutboxMessages.Add(new OutboxMessage
        {
            Id = id,
            EventType = PasswordRecoveryRequested.EventType,
            Payload = JsonSerializer.Serialize(value),
            CorrelationId = id.ToString(),
            CreatedAt = clock.GetUtcNow()
        });
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
