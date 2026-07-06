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

    [Fact]
    public void ToBookDetailsDto_ShouldMapCorrectly()
    {
        // Arrange
        var book = new Book
        {
            Id = 1,
            Title = "Test Book",
            Description = "Test Description",
            PublishedOn = new DateTime(2024, 1, 1),
            Publisher = "Test Publisher",
            Price = 100m,
            ImageName = "test.jpg",
            SoftDeleted = false,
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
                }
            ],
            Tags = [new Tag { Id = "tag1" }],
            Reviews =
            [
                new Review
                {
                    Id = 5,
                    Voter = "John Doe",
                    Stars = 4,
                    Comment = "Good book",
                    BookId = 1
                }
            ]
        };

        // Act
        var actual = BookMappings.ToBookDetailsDto.Compile()(book);

        // Assert
        actual.Should().NotBeNull();
        actual.Id.Should().Be(1);
        actual.Title.Should().Be("Test Book");
        actual.Description.Should().Be("Test Description");
        actual.PublishedOn.Should().Be(new DateTime(2024, 1, 1));
        actual.Publisher.Should().Be("Test Publisher");
        actual.Price.Should().Be(100m);
        actual.ImageName.Should().Be("test.jpg");
        actual.SoftDeleted.Should().BeFalse();

        actual.Promotion.Should().NotBeNull();
        actual.Promotion!.Id.Should().Be(1);

        actual.Authors.Should().HaveCount(1);
        actual.Authors.First().Should().BeEquivalentTo(new AuthorDto { Id = 10, Name = "Author 1" });

        actual.Tags.Should().HaveCount(1);
        actual.Tags.First().Should().BeEquivalentTo(new TagDto { Id = "tag1" });

        actual.Reviews.Should().HaveCount(1);
        var expectedReview = new ReviewDto
        {
            Id = 5,
            Voter = "John Doe",
            Stars = 4,
            Comment = "Good book",
            BookId = 1
        };
        actual.Reviews.First().Should().BeEquivalentTo(expectedReview);
        actual.AverageRating.Should().Be(4.0);
    }

    [Fact]
    public void ToBookSummary_WhenNoReviews_ShouldReturnNullAverageRating()
    {
        // Arrange
        var book = new Book
        {
            Id = 1,
            Title = "Test Book",
            Reviews = []
        };

        // Act
        var actual = BookMappings.ToBookSummary.Compile()(book);

        // Assert
        actual.Should().NotBeNull();
        actual.AverageRating.Should().BeNull();
    }

    [Fact]
    public void ToBookSummary_WhenNoPromotion_ShouldReturnNullPromotion()
    {
        // Arrange
        var book = new Book
        {
            Id = 1,
            Title = "Test Book",
            Promotion = null
        };

        // Act
        var actual = BookMappings.ToBookSummary.Compile()(book);

        // Assert
        actual.Should().NotBeNull();
        actual.Promotion.Should().BeNull();
    }
}
