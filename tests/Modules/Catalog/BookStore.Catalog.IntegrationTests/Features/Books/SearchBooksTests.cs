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
        result.Items.Should().NotBeEmpty();
    }
}