namespace BookStore.Catalog.Contracts.Dtos;

public class PriceOfferDto
{
    public int Id { get; set; }
    public decimal NewPrice { get; set; }
    public string Text { get; set; } = null!;
    public int BookId { get; set; }
}
