namespace BookStore.Identity.Infrastructure;

internal sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options)
    : IdentityUserDbContext(options)
{
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

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
    }
}
