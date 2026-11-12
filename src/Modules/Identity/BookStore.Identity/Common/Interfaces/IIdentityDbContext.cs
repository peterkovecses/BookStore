namespace BookStore.Identity.Common.Interfaces;

internal interface IIdentityDbContext
{
    DbSet<OutboxMessage> OutboxMessages { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
