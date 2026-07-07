namespace BookStore.Catalog.Domain.Entities;

internal class Author : EntityBase<int>
{
    public string Name { get; init; } = null!;

    public ICollection<BookAuthor> BooksLink { get; init; } = [];
}
