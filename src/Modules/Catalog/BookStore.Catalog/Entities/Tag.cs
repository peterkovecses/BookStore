namespace BookStore.Catalog.Entities;

internal class Tag : EntityBase<string>
{
    public ICollection<Book> Books { get; set; } = [];
}
