namespace BookStore.Catalog.Domain.Entities;

internal class Review : EntityBase<int>
{
    public string Voter { get; set; } = null!;
    public int Stars { get; set; }
    public string? Comment { get; set; }

    public int BookId { get; set; }
    public Book Book { get; set; } = null!;
}
