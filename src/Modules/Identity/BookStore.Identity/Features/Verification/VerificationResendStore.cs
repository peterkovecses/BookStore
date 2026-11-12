namespace BookStore.Identity.Features.Verification;

internal interface IVerificationResendStore
{
    Task<ResendVerificationOutcome> EnqueueAsync(string email, OutboxMessage message, CancellationToken cancellationToken);
}

internal sealed class VerificationResendStore(
    IIdentityDbContext context,
    ILookupNormalizer normalizer,
    TimeProvider clock) : IVerificationResendStore
{
    private const int CooldownSeconds = 60;

    public async Task<ResendVerificationOutcome> EnqueueAsync(string email, OutboxMessage message, CancellationToken cancellationToken)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizer.NormalizeEmail(email))));
        var cleanupTime = clock.GetUtcNow();
        await context.VerificationResendCooldowns.Where(cooldown => cooldown.ExpiresAt <= cleanupTime)
            .ExecuteDeleteAsync(cancellationToken);

        return await context.ExecuteInTransactionAsync(async (transaction, token) =>
        {
            await transaction.AcquireLockAsync($"identity.verification-resend:{hash}", token);
            var now = clock.GetUtcNow();
            var existing = await context.VerificationResendCooldowns.SingleOrDefaultAsync(
                cooldown => cooldown.EmailHash == hash, token);
            if (existing is not null && existing.ExpiresAt > now)
            {
                var remaining = (int)Math.Ceiling((existing.ExpiresAt - now).TotalSeconds);
                await transaction.CommitAsync(token);

                return new ResendVerificationOutcome(false, remaining);
            }

            if (existing is null)
            {
                existing = new VerificationResendCooldown { EmailHash = hash };
                context.VerificationResendCooldowns.Add(existing);
            }

            existing.ExpiresAt = now.AddSeconds(CooldownSeconds);
            context.OutboxMessages.Add(message);
            await context.SaveChangesAsync(token);
            await transaction.CommitAsync(token);

            return new ResendVerificationOutcome(true, CooldownSeconds);
        }, cancellationToken);
    }
}
