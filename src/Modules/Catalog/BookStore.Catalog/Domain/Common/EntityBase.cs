namespace BookStore.Catalog.Domain.Common;

internal abstract class EntityBase<TId> : IAuditableEntity 
    where TId : IEquatable<TId>
{
    public TId Id { get; set; } = default!;
    public DateTime CreatedUtc { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? LastModifiedUtc { get; set; }
    public string? LastModifiedBy { get; set; }
}
