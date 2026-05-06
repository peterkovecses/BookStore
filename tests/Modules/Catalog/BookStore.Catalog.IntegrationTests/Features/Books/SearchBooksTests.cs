namespace BookStore.Catalog.IntegrationTests.Features.Books;

public class SearchBooksTests(CatalogWebApplicationFactory factory) : CatalogIntegrationTestBase(factory)
{
    [Fact]
    public async Task Search_WhenNoFiltersApplied_ShouldReturnAllNonSoftDeletedBooks()
    {
        // Act
        var response = await Client.GetAsync("/api/catalog/books", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<PaginatedList<BookSummaryDto>>(TestContext.Current.CancellationToken);
        result.Should().NotBeNull();
        
        result.TotalCount.Should().Be(2);
        result.Items.Should().HaveCount(2);

        result.Items.Should().NotContain(b => b.Title == "Soft Deleted Book");

        // Book 1
        var bookWithImage = result.Items.Single(b => b.Title == "Active Book With Image");
        bookWithImage.ImageUrl.Should().NotBeNullOrWhiteSpace();
        bookWithImage.ImageUrl.Should().Contain("book-covers/image1.jpg"); // A várt URL struktúra alapján
        bookWithImage.Price.Should().Be(10.00m);

        // Book 2
        var bookWithoutImage = result.Items.Single(b => b.Title == "Active Book Without Image");
        bookWithoutImage.ImageUrl.Should().BeNull();
        bookWithoutImage.Price.Should().Be(20.00m);
    }
}