namespace BookStore.Catalog.IntegrationTests.Features.Books;

public class GetBookDetailsTests(CatalogWebApplicationFactory factory) : CatalogIntegrationTestBase(factory)
{
    [Fact]
    public async Task GetDetails_WhenBookExists_ShouldReturnBookDetails()
    {
        // Arrange
        Author author1 = new() { Name = "Author 1" };
        Author author2 = new() { Name = "Author 2" };
        Tag tag1 = new() { Id = "Tag 1" };
        Tag tag2 = new() { Id = "Tag 2" };

        Book book = CreateBook(
            title: "Detailed Book",
            description: "Deep description",
            publishedOn: new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            publisher: "Big Publisher",
            price: 45.00m,
            imageName: "detailed_cover.jpg",
            authors: [author1, author2],
            tags: [tag1, tag2],
            promotion: new PriceOffer { NewPrice = 35.00m, Text = "Special Offer" },
            reviews:
            [
                new Review { Stars = 5, Comment = "Masterpiece", Voter = "Voter 1" },
                new Review { Stars = 4, Comment = "Excellent", Voter = "Voter 2" }
            ]);

        await SeedAsync(author1, author2, tag1, tag2, book);
        await UploadImageAsync("detailed_cover.jpg");

        // Act
        var response = await Client.GetAsync($"/api/catalog/books/{book.Id}", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<BookDetailsDto>(TestContext.Current.CancellationToken);
        result.Should().NotBeNull();

        result.Id.Should().Be(book.Id);
        result.Title.Should().Be("Detailed Book");
        result.Description.Should().Be("Deep description");
        result.Price.Should().Be(45.00m);
        result.ImageUrl.Should().NotBeNullOrWhiteSpace();
        result.ImageUrl.Should().Contain(CatalogTestConstants.BookCoversContainerName + "/detailed_cover.jpg");

        // Authors
        result.Authors.Should().HaveCount(2);
        result.Authors.Should().Contain(a => a.Name == "Author 1");
        result.Authors.Should().Contain(a => a.Name == "Author 2");

        // Tags
        result.Tags.Should().HaveCount(2);
        result.Tags.Should().Contain(t => t.Id == "Tag 1");
        result.Tags.Should().Contain(t => t.Id == "Tag 2");

        // Promotion
        result.Promotion.Should().NotBeNull();
        result.Promotion!.NewPrice.Should().Be(35.00m);
        result.Promotion.Text.Should().Be("Special Offer");

        // Reviews
        result.Reviews.Should().HaveCount(2);
        result.Reviews.Should().Contain(r => r.Voter == "Voter 1" && r.Stars == 5);
        result.Reviews.Should().Contain(r => r.Voter == "Voter 2" && r.Stars == 4);
        result.AverageRating.Should().Be(4.5);
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
        // Act
        var response = await Client.GetAsync("/api/catalog/books/999", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
