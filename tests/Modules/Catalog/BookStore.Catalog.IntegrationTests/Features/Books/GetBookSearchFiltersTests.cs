namespace BookStore.Catalog.IntegrationTests.Features.Books;

public class GetBookSearchFiltersTests : CatalogIntegrationTestBase
{
    public GetBookSearchFiltersTests(CatalogWebApplicationFactory factory) : base(factory)
    {
        ClearCache();
    }

    private void ClearCache()
    {
        using var scope = Factory.Services.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Caching.Hybrid.HybridCache>();
        cache.RemoveAsync("book_search_filters").AsTask().GetAwaiter().GetResult();
    }

    [Fact]
    public async Task GetFilters_ShouldReturn200WithAuthorsAndTagIds()
    {
        // Arrange
        const string authorName1 = "Author A";
        const string authorName2 = "Author B";
        const string tagId1 = "Tag A";
        const string tagId2 = "Tag B";

        Author author1 = new() { Name = authorName1 };
        Author author2 = new() { Name = authorName2 };
        Tag tag1 = new() { Id = tagId1 };
        Tag tag2 = new() { Id = tagId2 };

        await SeedAsync(author1, author2, tag1, tag2);

        // Act
        var response = await Client.GetAsync("/api/catalog/books/filters", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<BookSearchFiltersDto>(TestContext.Current.CancellationToken);
        result.Should().NotBeNull();

        result!.TagIds.Should().HaveCount(2)
            .And.Contain(tagId1)
            .And.Contain(tagId2);

        result.Authors.Should().HaveCount(2);
        result.Authors.Should().Contain(a => a.Name == authorName1 && a.Id == author1.Id);
        result.Authors.Should().Contain(a => a.Name == authorName2 && a.Id == author2.Id);
    }

    [Fact]
    public async Task GetFilters_ShouldReturnAuthorsInAlphabeticalOrder()
    {
        // Arrange
        const string authorName1 = "Author C";
        const string authorName2 = "Author A";
        const string authorName3 = "Author B";

        Author author1 = new() { Name = authorName1 };
        Author author2 = new() { Name = authorName2 };
        Author author3 = new() { Name = authorName3 };

        await SeedAsync(author1, author2, author3);

        // Act
        var response = await Client.GetAsync("/api/catalog/books/filters", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<BookSearchFiltersDto>(TestContext.Current.CancellationToken);
        result.Should().NotBeNull();

        result!.Authors.Select(a => a.Name).Should().Equal(authorName2, authorName3, authorName1);
    }

    [Fact]
    public async Task GetFilters_WhenCalledTwice_ShouldReturnSameResultFromCache()
    {
        // Arrange
        const string authorName1 = "Author A";
        const string authorName2 = "Author B";
        const string tagId1 = "Tag A";

        Author author1 = new() { Name = authorName1 };
        Tag tag1 = new() { Id = tagId1 };

        await SeedAsync(author1, tag1);

        // Act
        var response1 = await Client.GetAsync("/api/catalog/books/filters", TestContext.Current.CancellationToken);
        var result1 = await response1.Content.ReadFromJsonAsync<BookSearchFiltersDto>(TestContext.Current.CancellationToken);

        Author author2 = new() { Name = authorName2 };
        await SeedAsync(author2);

        var response2 = await Client.GetAsync("/api/catalog/books/filters", TestContext.Current.CancellationToken);
        var result2 = await response2.Content.ReadFromJsonAsync<BookSearchFiltersDto>(TestContext.Current.CancellationToken);

        // Assert
        response1.StatusCode.Should().Be(HttpStatusCode.OK);
        result1.Should().NotBeNull();
        result1!.Authors.Should().ContainSingle(a => a.Name == authorName1);

        response2.StatusCode.Should().Be(HttpStatusCode.OK);
        result2.Should().NotBeNull();
        result2!.Authors.Should().ContainSingle(a => a.Name == authorName1)
            .And.NotContain(a => a.Name == authorName2);
    }

    [Fact]
    public async Task GetFilters_WhenNoAuthorsOrTagsExist_ShouldReturnEmptyCollections()
    {
        // Arrange

        // Act
        var response = await Client.GetAsync("/api/catalog/books/filters", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<BookSearchFiltersDto>(TestContext.Current.CancellationToken);
        result.Should().NotBeNull();
        result!.TagIds.Should().BeEmpty();
        result.Authors.Should().BeEmpty();
    }
}
