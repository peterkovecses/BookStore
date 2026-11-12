namespace BookStore.Identity.Infrastructure;

internal sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options)
    : IdentityUserDbContext(options), IIdentityDbContext
{
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<PasswordRecoveryRequestReceipt> PasswordRecoveryRequestReceipts => Set<PasswordRecoveryRequestReceipt>();

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
