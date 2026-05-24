namespace BookStore.Catalog.Contracts.Dtos;

public class BookDetailsDto
{
    public int Id { get; set; }
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;
    public DateTime PublishedOn { get; set; }
    public string Publisher { get; set; } = null!;
    public decimal Price { get; set; }

    [JsonIgnore]
    public string? ImageName { get; set; }
    public string? ImageUrl { get; set; }
    public bool SoftDeleted { get; set; }

    public PriceOfferDto? Promotion { get; set; }
    public IEnumerable<AuthorDto> Authors { get; set; } = null!;
    public IEnumerable<TagDto> Tags { get; set; } = null!;
    public ICollection<ReviewDto> Reviews { get; set; } = null!;
    public double? AverageRating { get; set; }
}
