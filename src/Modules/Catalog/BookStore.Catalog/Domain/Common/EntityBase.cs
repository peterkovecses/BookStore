namespace BookStore.Catalog.Domain.Common;

internal abstract class EntityBase<TId> : IAuditableEntity 
    where TId : IEquatable<TId>
{
    public TId Id { get; init; } = default!;
    public DateTime CreatedUtc { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTime? LastModifiedUtc { get; private set; }
    public string? LastModifiedBy { get; private set; }
}
