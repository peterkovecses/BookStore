namespace BookStore.Catalog.Domain.Entities;

internal class Book : EntityBase<int>
{
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;
    public DateTime PublishedOn { get; set; }
    public string Publisher { get; set; } = null!;
    public decimal Price { get; set; }
    public string? ImageName { get; set; }
    public bool SoftDeleted { get; set; }

    public PriceOffer? Promotion { get; set; }
    public ICollection<Review> Reviews { get; set; } = [];
    public ICollection<Tag> Tags { get; set; } = [];
    public ICollection<BookAuthor> AuthorsLink { get; set; } = [];
}
