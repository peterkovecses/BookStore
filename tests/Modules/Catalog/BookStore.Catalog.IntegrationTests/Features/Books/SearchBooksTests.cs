namespace BookStore.Catalog.IntegrationTests.Features.Books;

public class SearchBooksTests(CatalogWebApplicationFactory factory) : CatalogIntegrationTestBase(factory)
{
    [Fact]
    public async Task Search_WhenNoFiltersApplied_ShouldReturnAllNonSoftDeletedBooks()
    {
        // Arrange
        var fixedDate = new DateTime(2026, 5, 6, 0, 0, 0, DateTimeKind.Utc);
        
        Author author1 = new() { Name = "Author 1" };
        Author author2 = new() { Name = "Author 2" };
        Tag tag1 = new() { Id = "Tag 1" };
        Tag tag2 = new() { Id = "Tag 2" };

        Book book1 = CreateBook(
            title: "Active Book With Image",
            description: "Description 1",
            publishedOn: fixedDate,
            publisher: "Publisher 1",
            price: 10.00m,
            imageName: "image1.jpg",
            author: author1,
            tags: tag1);

        Book book2 = CreateBook(
            title: "Active Book Without Image",
            description: "Description 2",
            publishedOn: fixedDate,
            publisher: "Publisher 2",
            price: 20.00m,
            author: author2,
            tags: tag2);

        Book book3 = CreateBook(
            title: "Soft Deleted Book",
            description: "Description 3",
            publishedOn: fixedDate,
            publisher: "Publisher 3",
            price: 30.00m,
            imageName: "image3.jpg",
            softDeleted: true,
            author: author1);

        await SeedAsync(author1, author2, tag1, tag2, book1, book2, book3);
        await UploadImageAsync("image1.jpg");

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
        bookWithImage.ImageUrl.Should().Contain(CatalogTestConstants.BookCoversContainerName + "/image1.jpg");
        bookWithImage.Price.Should().Be(10.00m);

        // Book 2
        var bookWithoutImage = result.Items.Single(b => b.Title == "Active Book Without Image");
        bookWithoutImage.ImageUrl.Should().BeNull();
        bookWithoutImage.Price.Should().Be(20.00m);
    }

    [Fact]
    public async Task Search_WhenSearchTermMatchesTitle_ShouldReturnMatchingBooks()
    {
        // Arrange
        Book[] books =
        [
            CreateBook(title: "Domain Driven Design", publisher: "Publisher 1", price: 50.00m),
            CreateBook(title: "Domain Analysis", publisher: "Publisher 1", price: 45.00m),
            CreateBook(title: "Clean Code", publisher: "Publisher 2", price: 40.00m)
        ];

        await SeedAsync(books);

        // Act
        var response = await Client.GetAsync("/api/catalog/books?SearchTerm=Domain", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PaginatedList<BookSummaryDto>>(TestContext.Current.CancellationToken);
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(2);
        result.Items.Should().HaveCount(2);
        result.Items.Should().Contain(b => b.Title == "Domain Driven Design");
        result.Items.Should().Contain(b => b.Title == "Domain Analysis");
    }

    [Fact]
    public async Task Search_WhenAuthorFilterApplied_ShouldReturnOnlyMatchingBooks()
    {
        // Arrange
        Author author1 = new() { Name = "Author 1" };
        Author author2 = new() { Name = "Author 2" };
        
        Book book1 = CreateBook(title: "Book by Author 1", author: author1);
        Book book2 = CreateBook(title: "Book by Author 2", author: author2);

        await SeedAsync(author1, author2, book1, book2);

        // Act
        var response = await Client.GetAsync($"/api/catalog/books?AuthorIds={author1.Id}", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<PaginatedList<BookSummaryDto>>(TestContext.Current.CancellationToken);
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(1);
        result.Items.Should().ContainSingle(b => b.Title == "Book by Author 1");
    }

    [Fact]
    public async Task Search_WhenTagFilterApplied_ShouldReturnOnlyMatchingBooks()
    {
        // Arrange
        Tag tag1 = new() { Id = "Tag 1" };
        Tag tag2 = new() { Id = "Tag 2" };
        
        Book book1 = CreateBook(title: "Book with Tag 1", tags: tag1);
        Book book2 = CreateBook(title: "Book with Tag 2", tags: tag2);

        await SeedAsync(tag1, tag2, book1, book2);

        // Act
        var response = await Client.GetAsync($"/api/catalog/books?TagIds={tag1.Id}", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<PaginatedList<BookSummaryDto>>(TestContext.Current.CancellationToken);
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(1);
        result.Items.Should().ContainSingle(b => b.Title == "Book with Tag 1");
    }
}