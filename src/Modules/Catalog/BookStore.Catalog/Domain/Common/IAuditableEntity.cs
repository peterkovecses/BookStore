namespace BookStore.Catalog.Domain.Common;

internal interface IAuditableEntity
{
    DateTime CreatedUtc { get; }
    string? CreatedBy { get; }
    DateTime? LastModifiedUtc { get; }
    string? LastModifiedBy { get; }
}
