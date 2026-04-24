namespace BookStore.Catalog.Contracts.Dtos;

public class ReviewDto
{
    public int Id { get; set; }
    public string Voter { get; set; } = null!;
    public int Stars { get; set; }
    public string? Comment { get; set; }
    public int BookId { get; set; }
}
