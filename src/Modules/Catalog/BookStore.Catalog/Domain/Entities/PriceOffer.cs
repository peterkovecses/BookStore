namespace BookStore.Catalog.Domain.Entities;

internal class PriceOffer : EntityBase<int>
{
    public decimal NewPrice { get; init; }
    public string Text { get; init; } = null!;

    public int BookId { get; init; }
    public Book Book { get; init; } = null!;
}
