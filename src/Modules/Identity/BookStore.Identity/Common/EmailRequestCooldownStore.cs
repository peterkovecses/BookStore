namespace BookStore.Identity.Common;

internal enum EmailRequestPurpose
{
    Verification,
    PasswordRecovery
}

internal sealed record EmailRequestOutcome(bool Accepted, int RetryAfterSeconds);

internal interface IEmailRequestCooldownStore
{
    Task<EmailRequestOutcome> EnqueueAsync(
        EmailRequestPurpose purpose, string email, OutboxMessage message, CancellationToken cancellationToken);
}

internal sealed class EmailRequestCooldownStore(
    IIdentityDbContext context,
    ILookupNormalizer normalizer,
    TimeProvider clock) : IEmailRequestCooldownStore
{
    private const int CooldownSeconds = 60;

    public async Task<EmailRequestOutcome> EnqueueAsync(
        EmailRequestPurpose purpose, string email, OutboxMessage message, CancellationToken cancellationToken)
    {
        var normalizedEmail = normalizer.NormalizeEmail(email);
        // Keep existing verification hashes unchanged while separating recovery cooldowns.
        var key = purpose switch
        {
            EmailRequestPurpose.Verification => normalizedEmail,
            EmailRequestPurpose.PasswordRecovery => $"password-recovery:{normalizedEmail}",
            _ => throw new ArgumentOutOfRangeException(nameof(purpose))
        };
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
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

                return new EmailRequestOutcome(false, remaining);
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

            return new EmailRequestOutcome(true, CooldownSeconds);
        }, cancellationToken);
    }
}
