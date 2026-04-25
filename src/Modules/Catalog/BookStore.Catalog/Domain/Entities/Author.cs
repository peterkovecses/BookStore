namespace BookStore.Catalog.Domain.Entities;

internal class Author : EntityBase<int>
{
    public string Name { get; set; } = null!;

    public ICollection<BookAuthor> BooksLink { get; set; } = [];
}
