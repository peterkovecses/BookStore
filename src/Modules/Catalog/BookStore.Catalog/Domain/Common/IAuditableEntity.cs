namespace BookStore.Catalog.Domain.Common;

internal interface IAuditableEntity
{
    DateTime CreatedUtc { get; set; }
    string? CreatedBy { get; set; }
    DateTime? LastModifiedUtc { get; set; }
    string? LastModifiedBy { get; set; }
}
