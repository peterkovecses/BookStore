namespace BookStore.Catalog.Domain.Entities;

internal class Review : EntityBase<int>
{
    public string Voter { get; init; } = null!;
    public int Stars { get; init; }
    public string? Comment { get; init; }

    public int BookId { get; init; }
    public Book Book { get; init; } = null!;
}
