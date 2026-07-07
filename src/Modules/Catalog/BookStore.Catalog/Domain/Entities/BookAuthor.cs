namespace BookStore.Catalog.Domain.Entities;

internal class BookAuthor
{
    public int BookId { get; init; }
    public int AuthorId { get; init; }
    public byte Order { get; init; }

    public Book Book { get; init; } = null!;
    public Author Author { get; init; } = null!;
}
