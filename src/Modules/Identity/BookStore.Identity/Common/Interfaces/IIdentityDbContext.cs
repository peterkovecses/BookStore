namespace BookStore.Identity.Common.Interfaces;

internal interface IIdentityDbContext
{
    DbSet<ApplicationUser> Users { get; }
    DbSet<OutboxMessage> OutboxMessages { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<PasswordRecoveryRequestReceipt> PasswordRecoveryRequestReceipts { get; }
    DbSet<VerificationResendCooldown> VerificationResendCooldowns { get; }

    void ClearChangeTracker();
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
    Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<IIdentityDbTransaction, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken);
    Task<int> ExecuteSqlInterpolatedAsync(FormattableString sql, CancellationToken cancellationToken);
    Task<TResult> UseConnectionAsync<TResult>(
        Func<DbConnection, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken);
}

internal interface IIdentityDbTransaction
{
    Task CommitAsync(CancellationToken cancellationToken);
    Task RollbackAsync(CancellationToken cancellationToken);
    Task AcquireLockAsync(string resource, CancellationToken cancellationToken);
    Task<int?> TryAcquireLockAsync(string resource, int timeoutMilliseconds, CancellationToken cancellationToken);
}
