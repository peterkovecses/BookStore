namespace BookStore.Catalog.Domain.Entities;

internal class BookAuthor
{
    public int BookId { get; set; }
    public int AuthorId { get; set; }
    public byte Order { get; set; }

    public Book Book { get; set; } = null!;
    public Author Author { get; set; } = null!;
}
