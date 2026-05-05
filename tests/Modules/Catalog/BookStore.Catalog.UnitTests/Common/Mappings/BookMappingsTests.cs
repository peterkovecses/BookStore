namespace BookStore.Catalog.UnitTests.Common.Mappings;

public class BookMappingsTests
{
    [Fact]
    public void ToBookSummary_ShouldMapCorrectly()
    {
        // Arrange
        var book = new Book
        {
            Id = 1,
            Title = "Test Book",
            PublishedOn = new DateTime(2024, 1, 1),
            Publisher = "Test Publisher",
            Price = 100m,
            ImageName = "test.jpg",
            Promotion = new PriceOffer
            {
                Id = 1,
                NewPrice = 80m,
                Text = "Sale",
                BookId = 1
            },
            AuthorsLink =
            [
                new BookAuthor
                {
                    AuthorId = 10,
                    Author = new Author { Id = 10, Name = "Author 1" }
                },
                new BookAuthor
                {
                    AuthorId = 12,
                    Author = new Author { Id = 12, Name = "Author 2" }
                }
            ],
            Tags = [new Tag { Id = "tag1" }],
            Reviews =
            [
                new Review { Id = 1, Stars = 4 },
                new Review { Id = 2, Stars = 5 }
            ]
        };

        // Act
        var actual = BookMappings.ToBookSummary.Compile()(book);

        // Assert
        actual.Should().NotBeNull();
        actual.Id.Should().Be(1);
        actual.Title.Should().Be("Test Book");
        actual.PublishedOn.Should().Be(new DateTime(2024, 1, 1));
        actual.Publisher.Should().Be("Test Publisher");
        actual.Price.Should().Be(100m);
        actual.ImageName.Should().Be("test.jpg");
        
        actual.Promotion.Should().NotBeNull();
        actual.Promotion!.Id.Should().Be(1);
        actual.Promotion.NewPrice.Should().Be(80m);
        actual.Promotion.Text.Should().Be("Sale");
        actual.Promotion.BookId.Should().Be(1);
        
        actual.Authors.Should().HaveCount(2);
        actual.Authors.First().Should().BeEquivalentTo(new AuthorDto { Id = 10, Name = "Author 1" });
        actual.Authors.Last().Should().BeEquivalentTo(new AuthorDto { Id = 12, Name = "Author 2" });
        
        actual.Tags.Should().HaveCount(1);
        actual.Tags.First().Should().BeEquivalentTo(new TagDto { Id = "tag1" });
        
        actual.ReviewCount.Should().Be(2);
        actual.AverageRating.Should().Be(4.5);
    }
}
