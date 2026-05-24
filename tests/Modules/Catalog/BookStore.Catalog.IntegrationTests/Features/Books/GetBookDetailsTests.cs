namespace BookStore.Catalog.IntegrationTests.Features.Books;

public class GetBookDetailsTests(CatalogWebApplicationFactory factory) : CatalogIntegrationTestBase(factory)
{
    [Fact]
    public async Task GetDetails_WhenBookExists_ShouldReturnBookDetails()
    {
        // Arrange
        const string authorName1 = "Author 1";
        const string authorName2 = "Author 2";
        const string tagName1 = "Tag 1";
        const string tagName2 = "Tag 2";
        const string bookTitle = "Detailed Book";
        const string bookDescription = "Deep description";
        const string bookPublisher = "Big Publisher";
        const decimal bookPrice = 45.00m;
        const string imageName = "detailed_cover.jpg";
        var publishedOn = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        const decimal promotionPrice = 35.00m;
        const string promotionText = "Special Offer";

        const int reviewStars1 = 5;
        const string reviewComment1 = "Masterpiece";
        const string reviewVoter1 = "Voter 1";

        const int reviewStars2 = 4;
        const string reviewComment2 = "Excellent";
        const string reviewVoter2 = "Voter 2";

        const double expectedAverageRating = 4.5;

        Author author1 = new() { Name = authorName1 };
        Author author2 = new() { Name = authorName2 };
        Tag tag1 = new() { Id = tagName1 };
        Tag tag2 = new() { Id = tagName2 };

        Book book = CreateBook(
            title: bookTitle,
            description: bookDescription,
            publishedOn: publishedOn,
            publisher: bookPublisher,
            price: bookPrice,
            imageName: imageName,
            authors: [author1, author2],
            tags: [tag1, tag2],
            promotion: new PriceOffer { NewPrice = promotionPrice, Text = promotionText },
            reviews:
            [
                new Review { Stars = reviewStars1, Comment = reviewComment1, Voter = reviewVoter1 },
                new Review { Stars = reviewStars2, Comment = reviewComment2, Voter = reviewVoter2 }
            ]);

        await SeedAsync(author1, author2, tag1, tag2, book);
        await UploadImageAsync(imageName);

        // Act
        var response = await Client.GetAsync($"/api/catalog/books/{book.Id}", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<BookDetailsDto>(TestContext.Current.CancellationToken);
        result.Should().NotBeNull();

        result!.Id.Should().Be(book.Id);
        result.Title.Should().Be(bookTitle);
        result.Description.Should().Be(bookDescription);
        result.Price.Should().Be(bookPrice);
        result.ImageUrl.Should().NotBeNullOrWhiteSpace();
        result.ImageUrl.Should().Contain(CatalogTestConstants.BookCoversContainerName + "/" + imageName);

        // Authors
        result.Authors.Should().HaveCount(2);
        result.Authors.Should().Contain(a => a.Name == authorName1);
        result.Authors.Should().Contain(a => a.Name == authorName2);

        // Tags
        result.Tags.Should().HaveCount(2);
        result.Tags.Should().Contain(t => t.Id == tagName1);
        result.Tags.Should().Contain(t => t.Id == tagName2);

        // Promotion
        result.Promotion.Should().NotBeNull();
        result.Promotion!.NewPrice.Should().Be(promotionPrice);
        result.Promotion.Text.Should().Be(promotionText);

        // Reviews
        result.Reviews.Should().HaveCount(2);
        result.Reviews.Should().Contain(r => r.Voter == reviewVoter1 && r.Stars == reviewStars1);
        result.Reviews.Should().Contain(r => r.Voter == reviewVoter2 && r.Stars == reviewStars2);
        result.AverageRating.Should().Be(expectedAverageRating);
    }

    [Fact]
    public async Task GetDetails_WhenBookHasNoImage_ShouldReturnNullImageUrl()
    {
        // Arrange
        Book book = CreateBook(title: "Book Without Image", imageName: null);
        await SeedAsync(book);

        // Act
        var response = await Client.GetAsync($"/api/catalog/books/{book.Id}", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<BookDetailsDto>(TestContext.Current.CancellationToken);
        result.Should().NotBeNull();
        result!.ImageUrl.Should().BeNull();
    }

    [Fact]
    public async Task GetDetails_WhenBookDoesNotExist_ShouldReturnNotFound()
    {
        // Arrange
        const int nonExistentBookId = 999;

        // Act
        var response = await Client.GetAsync($"/api/catalog/books/{nonExistentBookId}", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
