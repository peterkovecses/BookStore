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

    [Theory]
    [InlineData("Domain", new[] { "Domain Driven Design", "Domain Analysis" })]
    [InlineData("UniqueDescription", new[] { "Target Book 1" })]
    [InlineData("UniquePublisher", new[] { "Target Book 1" })]
    [InlineData("UniqueAuthor", new[] { "Target Book 1", "Target Book 2" })]
    [InlineData("NonExistent", new string[] { })]
    public async Task Search_WhenSearchTermMatches_ShouldReturnMatchingBooks(string searchTerm, string[] expectedTitles)
    {
        // Arrange
        Author author1 = new() { Name = "UniqueAuthor" };
        Author author2 = new() { Name = "Another Author" };

        Book[] books =
        [
            CreateBook(title: "Domain Driven Design", publisher: "Publisher 1"),
            CreateBook(title: "Domain Analysis", publisher: "Publisher 1"),
            CreateBook(title: "Clean Code", publisher: "Publisher 2"),
            CreateBook(
                title: "Target Book 1",
                description: "This is a UniqueDescription",
                publisher: "UniquePublisher",
                author: author1),
            CreateBook(
                title: "Target Book 2",
                description: "Common description",
                publisher: "Common Publisher",
                author: author1),
            CreateBook(
                title: "Irrelevant Book",
                author: author2)
        ];

        await SeedAsync(author1, author2, books);

        // Act
        var response = await Client.GetAsync($"/api/catalog/books?SearchTerm={searchTerm}", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PaginatedList<BookSummaryDto>>(TestContext.Current.CancellationToken);
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(expectedTitles.Length);
        result.Items.Should().HaveCount(expectedTitles.Length);
        result.Items.Select(b => b.Title).Should().BeEquivalentTo(expectedTitles);
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

    [Fact]
    public async Task Search_WhenMultipleAuthorsAndTagsApplied_ShouldReturnMatchingBooks()
    {
        // Arrange
        Author author1 = new() { Name = "Author 1" };
        Author author2 = new() { Name = "Author 2" };
        Author author3 = new() { Name = "Author 3" };

        Tag tag1 = new() { Id = "Tag 1" };
        Tag tag2 = new() { Id = "Tag 2" };
        Tag tag3 = new() { Id = "Tag 3" };

        Book book1 = CreateBook(title: "Book 1", author: author1, tags: tag1);
        Book book2 = CreateBook(title: "Book 2", author: author2, tags: tag2);
        Book book3 = CreateBook(title: "Book 3", author: author3, tags: tag3);

        await SeedAsync(author1, author2, author3, tag1, tag2, tag3, book1, book2, book3);

        // Act
        var response = await Client.GetAsync($"/api/catalog/books?AuthorIds={author1.Id}&AuthorIds={author2.Id}&TagIds={tag1.Id}&TagIds={tag2.Id}", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PaginatedList<BookSummaryDto>>(TestContext.Current.CancellationToken);
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(2);
        result.Items.Should().HaveCount(2);
        result.Items.Should().Contain(b => b.Title == "Book 1");
        result.Items.Should().Contain(b => b.Title == "Book 2");
    }

    [Fact]
    public async Task Search_WhenAllFiltersApplied_ShouldReturnOnlyMatchingBooks()
    {
        // Arrange
        Author author1 = new() { Name = "Author 1" };
        Tag tag1 = new() { Id = "Tag 1" };

        Book book1 = CreateBook(title: "Target Book", author: author1, tags: tag1);
        Book book2 = CreateBook(title: "Target Book but wrong author", tags: tag1);
        Book book3 = CreateBook(title: "Target Book but wrong tag", author: author1);

        await SeedAsync(author1, tag1, book1, book2, book3);

        // Act
        var response = await Client.GetAsync($"/api/catalog/books?SearchTerm=Target&AuthorIds={author1.Id}&TagIds={tag1.Id}", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PaginatedList<BookSummaryDto>>(TestContext.Current.CancellationToken);
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(1);
        result.Items.Should().ContainSingle(b => b.Title == "Target Book");
    }

    [Fact]
    public async Task Search_WhenPaginationApplied_ShouldReturnCorrectPage()
    {
        // Arrange
        const int totalBooks = 10;
        const int pageNumber = 2;
        const int pageSize = 3;
        const int expectedTotalPages = 4;
        string[] expectedTitles = ["Book 04", "Book 05", "Book 06"];

        Book[] books = [.. Enumerable.Range(1, totalBooks).Select(i => CreateBook(title: $"Book {i:D2}"))];

        await SeedAsync(books);

        // Act
        var response = await Client.GetAsync($"/api/catalog/books?PageNumber={pageNumber}&PageSize={pageSize}", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PaginatedList<BookSummaryDto>>(TestContext.Current.CancellationToken);
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(totalBooks);
        result.Items.Should().HaveCount(pageSize);
        result.PageNumber.Should().Be(pageNumber);
        result.TotalPages.Should().Be(expectedTotalPages);
        result.Items.Select(b => b.Title).Should().Equal(expectedTitles);
    }

    [Theory]
    [InlineData("Title", false, new[] { "A Book", "B Book", "C Book" })]
    [InlineData("Title", true, new[] { "C Book", "B Book", "A Book" })]
    [InlineData("Price", false, new[] { "B Book", "A Book", "C Book" })]
    public async Task Search_WhenSortingApplied_ShouldReturnBooksInCorrectOrder(string sortBy, bool isDescending, string[] expectedOrder)
    {
        // Arrange
        Book[] books =
        [
            CreateBook(title: "A Book", price: 20.00m),
            CreateBook(title: "B Book", price: 10.00m),
            CreateBook(title: "C Book", price: 30.00m)
        ];

        await SeedAsync(books);

        // Act
        var response = await Client.GetAsync($"/api/catalog/books?SortBy={sortBy}&IsDescending={isDescending}", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PaginatedList<BookSummaryDto>>(TestContext.Current.CancellationToken);
        result.Should().NotBeNull();
        result.Items.Select(b => b.Title).Should().Equal(expectedOrder);
    }

    [Fact]
    public async Task Search_ShouldReturnCorrectCalculatedAndLinkedData()
    {
        // Arrange
        Author author1 = new() { Name = "Author 1" };
        Author author2 = new() { Name = "Author 2" };
        Tag tag1 = new() { Id = "Tag 1" };
        Tag tag2 = new() { Id = "Tag 2" };

        Book book = CreateBook(
            title: "Calculated Data Book",
            price: 100.00m,
            author: author1,
            tags: tag1);

        // Add second author and tag
        book.AuthorsLink.Add(new BookAuthor { Author = author2, Order = 2 });
        book.Tags.Add(tag2);

        // Add promotion
        book.Promotion = new PriceOffer { NewPrice = 80.00m, Text = "Sale!" };

        // Add reviews
        book.Reviews.Add(new Review { Stars = 4, Comment = "Good", Voter = "Voter 1" });
        book.Reviews.Add(new Review { Stars = 5, Comment = "Great", Voter = "Voter 2" });

        await SeedAsync(author1, author2, tag1, tag2, book);

        // Act
        var response = await Client.GetAsync("/api/catalog/books?SearchTerm=Calculated", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PaginatedList<BookSummaryDto>>(TestContext.Current.CancellationToken);
        result.Should().NotBeNull();
        result.Items.Should().ContainSingle();

        var bookDto = result.Items.Single();

        // Authors and Tags
        bookDto.Authors.Should().HaveCount(2);
        bookDto.Authors.Should().Contain(a => a.Name == "Author 1");
        bookDto.Authors.Should().Contain(a => a.Name == "Author 2");
        bookDto.Tags.Should().HaveCount(2);
        bookDto.Tags.Should().Contain(t => t.Id == "Tag 1");
        bookDto.Tags.Should().Contain(t => t.Id == "Tag 2");

        // Promotion
        bookDto.Promotion.Should().NotBeNull();
        bookDto.Promotion!.NewPrice.Should().Be(80.00m);
        bookDto.Promotion.Text.Should().Be("Sale!");

        // Reviews
        bookDto.ReviewCount.Should().Be(2);
        bookDto.AverageRating.Should().Be(4.5);
    }

    [Theory]
    [InlineData("PageNumber", 0)]
    [InlineData("PageNumber", -1)]
    [InlineData("PageSize", 0)]
    [InlineData("PageSize", 101)]
    public async Task Search_WhenPaginationParametersAreInvalid_ShouldReturnBadRequest(string parameterName, int value)
    {
        // Act
        var response = await Client.GetAsync($"/api/catalog/books?{parameterName}={value}", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problemDetails = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestContext.Current.CancellationToken);
        problemDetails.Should().NotBeNull();
        problemDetails.Errors.Should().ContainKey(parameterName);
    }

    [Fact]
    public async Task Search_WhenSearchTermIsTooLong_ShouldReturnBadRequest()
    {
        // Arrange
        string longSearchTerm = new('a', 101);

        // Act
        var response = await Client.GetAsync($"/api/catalog/books?SearchTerm={longSearchTerm}", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problemDetails = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestContext.Current.CancellationToken);
        problemDetails.Should().NotBeNull();
        problemDetails.Errors.Should().ContainKey("Filter.SearchTerm");
    }
}