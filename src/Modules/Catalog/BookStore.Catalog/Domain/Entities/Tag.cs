namespace BookStore.Catalog.Domain.Entities;

internal class Tag : EntityBase<string>
{
    public ICollection<Book> Books { get; set; } = [];
}
