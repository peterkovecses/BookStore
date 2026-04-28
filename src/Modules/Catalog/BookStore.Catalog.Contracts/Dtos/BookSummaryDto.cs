namespace BookStore.Catalog.Contracts.Dtos;

public class BookSummaryDto
{
    public int Id { get; set; }
    public string Title { get; set; } = null!;
    public DateTime PublishedOn { get; set; }
    public string Publisher { get; set; } = null!;
    public decimal Price { get; set; }

    [JsonIgnore]
    public string? ImageName { get; set; }
    public string? ImageUrl { get; set; }

    public PriceOfferDto? Promotion { get; set; }
    public IEnumerable<AuthorDto> Authors { get; set; } = null!;
    public IEnumerable<TagDto> Tags { get; set; } = null!;
    public int ReviewCount { get; set; }
    public double? AverageRating { get; set; }
}
