namespace BookStore.Catalog.Infrastructure.Persistence.Interceptors;

internal sealed class AuditableEntityInterceptor(TimeProvider dateTime) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        UpdateEntities(eventData.Context);
        
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        UpdateEntities(eventData.Context);
        
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void UpdateEntities(DbContext? context)
    {
        if (context is null) return;

        var utcNow = dateTime.GetUtcNow().DateTime;
        const string currentUserId = "System";

        foreach (var entry in context.ChangeTracker.Entries<IAuditableEntity>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                if (entry.State == EntityState.Added)
                {
                    entry.Property(x => x.CreatedUtc).CurrentValue = utcNow;
                    entry.Property(x => x.CreatedBy).CurrentValue = currentUserId;
                }

                entry.Property(x => x.LastModifiedUtc).CurrentValue = utcNow;
                entry.Property(x => x.LastModifiedBy).CurrentValue = currentUserId;
            }
        }
    }
}
