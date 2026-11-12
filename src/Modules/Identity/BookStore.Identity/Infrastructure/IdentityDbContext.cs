namespace BookStore.Identity.Infrastructure;

internal sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options)
    : IdentityUserDbContext(options), IIdentityDbContext
{
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PasswordRecoveryRequestReceipt> PasswordRecoveryRequestReceipts => Set<PasswordRecoveryRequestReceipt>();

    public void ClearChangeTracker() => ChangeTracker.Clear();

    public Task<int> ExecuteSqlInterpolatedAsync(FormattableString sql, CancellationToken cancellationToken) =>
        Database.ExecuteSqlInterpolatedAsync(sql, cancellationToken);

    public Task<TResult> UseConnectionAsync<TResult>(
        Func<DbConnection, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken) =>
        UseConnectionCoreAsync(operation, cancellationToken);

    public Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<IIdentityDbTransaction, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken) =>
        Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            ChangeTracker.Clear();
            await using var transaction = await Database.BeginTransactionAsync(cancellationToken);
            var identityTransaction = new IdentityDbTransaction(Database.GetDbConnection(), transaction);

            return await operation(identityTransaction, cancellationToken);
        });

    private async Task<TResult> UseConnectionCoreAsync<TResult>(
        Func<DbConnection, CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken)
    {
        await Database.OpenConnectionAsync(cancellationToken);
        try
        {
            return await operation(Database.GetDbConnection(), cancellationToken);
        }
        finally
        {
            await Database.CloseConnectionAsync();
        }
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.HasDefaultSchema("identity");
        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(user => user.FirstName).HasMaxLength(50).IsRequired();
            entity.Property(user => user.LastName).HasMaxLength(50).IsRequired();
            entity.HasIndex(user => user.NormalizedEmail)
                .HasDatabaseName("EmailIndex")
                .IsUnique()
                .HasFilter("[NormalizedEmail] IS NOT NULL");
        });
        builder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable("OutboxMessages");
            entity.HasKey(message => message.Id);
            entity.Property(message => message.EventType).HasMaxLength(200).IsRequired();
            entity.Property(message => message.Payload).IsRequired();
            entity.Property(message => message.CorrelationId).HasMaxLength(128).IsRequired();
            entity.Property(message => message.LastError).HasMaxLength(500);
            entity.Property(message => message.LastReplayedBy).HasMaxLength(256);
            entity.HasIndex(message => new { message.Status, message.NextAttemptAt, message.CreatedAt });
            entity.HasIndex(message => new { message.Status, message.CompletedAt });
        });
        builder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("RefreshTokens");
            entity.HasKey(token => token.Hash);
            entity.Property(token => token.Hash).HasMaxLength(64).IsUnicode(false);
            entity.Property(token => token.UserId).HasMaxLength(450).IsRequired();
            entity.HasIndex(token => token.FamilyId);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(token => token.UserId).OnDelete(DeleteBehavior.Cascade);
        });
        builder.Entity<PasswordRecoveryRequestReceipt>(entity =>
        {
            entity.ToTable("PasswordRecoveryRequestReceipts");
            entity.HasKey(receipt => receipt.RequestEventId);
            entity.HasOne<OutboxMessage>().WithOne()
                .HasForeignKey<PasswordRecoveryRequestReceipt>(receipt => receipt.RequestEventId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

internal sealed class IdentityDbTransaction(DbConnection connection, IDbContextTransaction transaction) : IIdentityDbTransaction
{
    public Task CommitAsync(CancellationToken cancellationToken) => transaction.CommitAsync(cancellationToken);

    public Task RollbackAsync(CancellationToken cancellationToken) => transaction.RollbackAsync(cancellationToken);

    public Task AcquireLockAsync(string resource, CancellationToken cancellationToken) =>
        ExecuteLockAsync(resource, 15_000, cancellationToken, """
            DECLARE @result int;
            EXEC @result = sp_getapplock @Resource = @resource, @LockMode = 'Exclusive',
                @LockOwner = 'Transaction', @LockTimeout = @timeout;
            IF @result < 0 THROW 51000, 'Could not acquire identity lock.', 1;
            """);

    public async Task<int?> TryAcquireLockAsync(string resource, int timeoutMilliseconds, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandText = """
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock @Resource = @resource, @LockMode = 'Exclusive',
                @LockOwner = 'Transaction', @LockTimeout = @timeout;
            SELECT @result;
            """;
        AddParameter(command, "@resource", resource);
        AddParameter(command, "@timeout", timeoutMilliseconds);
        var result = await command.ExecuteScalarAsync(cancellationToken);

        return result is int lockResult ? lockResult : null;
    }

    private async Task ExecuteLockAsync(
        string resource,
        int timeoutMilliseconds,
        CancellationToken cancellationToken,
        string commandText)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandText = commandText;
        AddParameter(command, "@resource", resource);
        AddParameter(command, "@timeout", timeoutMilliseconds);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
