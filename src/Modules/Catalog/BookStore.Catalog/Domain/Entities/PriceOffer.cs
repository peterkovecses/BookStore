namespace BookStore.Catalog.Domain.Entities;

internal class PriceOffer : EntityBase<int>
{
    public decimal NewPrice { get; set; }
    public string Text { get; set; } = null!;

    public int BookId { get; set; }
    public Book Book { get; set; } = null!;
}
