namespace BookStore.Identity.Common;

internal enum EmailRequestPurpose
{
    Verification,
    PasswordRecovery
}

internal sealed record EmailRequestOutcome(bool Accepted, int RetryAfterSeconds);

internal interface IEmailRequestCooldownStore
{
    /// <summary>
    /// Enqueues an email request unless the address is still within the cooldown for the given purpose.
    /// </summary>
    /// <param name="purpose">The request purpose, with a separate cooldown for each purpose.</param>
    /// <param name="email">The address used to identify the cooldown; it is normalized before lookup.</param>
    /// <param name="message">The outbox message to add when the request is accepted.</param>
    /// <param name="cancellationToken">A token used to cancel the operation.</param>
    /// <returns>
    /// An outcome indicating whether the message was enqueued and the cooldown or remaining wait in seconds.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="purpose"/> is not supported.</exception>
    /// <exception cref="OperationCanceledException">The operation was canceled.</exception>
    Task<EmailRequestOutcome> EnqueueAsync(
        EmailRequestPurpose purpose, string email, OutboxMessage message, CancellationToken cancellationToken);
}

internal sealed class EmailRequestCooldownStore(
    IIdentityDbContext context,
    ILookupNormalizer normalizer,
    TimeProvider clock) : IEmailRequestCooldownStore
{
    private const int CooldownSeconds = 60;

    /// <inheritdoc />
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
