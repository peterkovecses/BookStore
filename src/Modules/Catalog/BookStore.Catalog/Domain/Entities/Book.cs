namespace BookStore.Catalog.Domain.Entities;

internal class Book : EntityBase<int>
{
    public string Title { get; init; } = null!;
    public string Description { get; init; } = null!;
    public DateTime PublishedOn { get; init; }
    public string Publisher { get; init; } = null!;
    public decimal Price { get; init; }
    public string? ImageName { get; init; }
    public bool SoftDeleted { get; init; }

    public PriceOffer? Promotion { get; internal set; }
    public ICollection<Review> Reviews { get; init; } = [];
    public ICollection<Tag> Tags { get; init; } = [];
    public ICollection<BookAuthor> AuthorsLink { get; init; } = [];
}
