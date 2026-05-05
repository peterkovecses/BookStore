namespace BookStore.Catalog.UnitTests.Common.Mappings;

public class BookMappingsTests
{
    [Fact]
    public void ToBookSummary_ShouldMapCorrectly()
    {
        // Arrange
        var author = new Author { Id = 1, Name = "Test Author" };
        var book = new Book
        {
            Id = 1,
            Title = "Test Book",
            PublishedOn = new DateTime(2026, 1, 1),
            Publisher = "Test Publisher",
            Price = 100,
            ImageName = "test.jpg",
            Promotion = new PriceOffer
            {
                Id = 1,
                NewPrice = 80,
                Text = "Sale",
                BookId = 1
            },
            AuthorsLink = [new BookAuthor { AuthorId = author.Id, Author = author }],
            Reviews = [new Review { Stars = 5 }, new Review { Stars = 4 }],
            Tags = [new Tag { Id = "Tag1" }]
        };

        // Act
        var sut = BookMappings.ToBookSummary.Compile();
        var actual = sut(book);

        // Assert
        actual.Id.Should().Be(book.Id);
        actual.Title.Should().Be(book.Title);
        actual.PublishedOn.Should().Be(book.PublishedOn);
        actual.Publisher.Should().Be(book.Publisher);
        actual.Price.Should().Be(book.Price);
        actual.ImageName.Should().Be(book.ImageName);
        
        actual.Promotion.Should().NotBeNull();
        actual.Promotion!.NewPrice.Should().Be(book.Promotion.NewPrice);
        
        actual.Authors.Should().HaveCount(1);
        actual.Authors.First().Name.Should().Be("Test Author");
        
        actual.Tags.Should().HaveCount(1);
        actual.Tags.First().Id.Should().Be("Tag1");
        
        actual.ReviewCount.Should().Be(2);
        actual.AverageRating.Should().Be(4.5);
    }
}
