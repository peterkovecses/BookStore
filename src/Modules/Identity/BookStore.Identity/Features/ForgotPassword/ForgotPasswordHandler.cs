namespace BookStore.Identity.Features.ForgotPassword;

internal sealed class ForgotPasswordHandler(IdentityDbContext context, IDataProtectionProvider protection,
    IOptions<PasswordRecoveryOptions> options, TimeProvider clock)
    : IRequestHandler<ForgotPasswordCommand, Result>
{
    public async Task<Result> HandleAsync(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();
        var now = clock.GetUtcNow();
        var value = new PasswordRecoveryRequested(
            protection.CreateProtector(PasswordRecoveryRequested.ProtectionPurpose).Protect(request.Email),
            now + options.Value.TokenLifetime);
        context.OutboxMessages.Add(new OutboxMessage
        {
            Id = id,
            EventType = PasswordRecoveryRequested.EventType,
            Payload = JsonSerializer.Serialize(value),
            CorrelationId = id.ToString(),
            CreatedAt = now
        });
        await context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
