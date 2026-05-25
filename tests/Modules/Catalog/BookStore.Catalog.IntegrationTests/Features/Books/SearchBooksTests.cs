namespace BookStore.Catalog.IntegrationTests.Features.Books;

public class SearchBooksTests(CatalogWebApplicationFactory factory) : CatalogIntegrationTestBase(factory)
{
    [Fact]
    public async Task Search_WhenNoFiltersApplied_ShouldReturnAllNonSoftDeletedBooks()
    {
        // Arrange
        var fixedDate = new DateTime(2026, 5, 6, 0, 0, 0, DateTimeKind.Utc);
        const string authorName1 = "Author 1";
        const string authorName2 = "Author 2";
        const string tagName1 = "Tag 1";
        const string tagName2 = "Tag 2";
        const string bookTitleWithImage = "Active Book With Image";
        const string bookTitleWithoutImage = "Active Book Without Image";
        const string bookTitleSoftDeleted = "Soft Deleted Book";
        const string imageName1 = "image1.jpg";
        const string imageName3 = "image3.jpg";
        const decimal bookPrice1 = 10.00m;
        const decimal bookPrice2 = 20.00m;
        const decimal bookPrice3 = 30.00m;

        Author author1 = new() { Name = authorName1 };
        Author author2 = new() { Name = authorName2 };
        Tag tag1 = new() { Id = tagName1 };
        Tag tag2 = new() { Id = tagName2 };

        Book book1 = CreateBook(
            title: bookTitleWithImage,
            description: "Description 1",
            publishedOn: fixedDate,
            publisher: "Publisher 1",
            price: bookPrice1,
            imageName: imageName1,
            authors: [author1],
            tags: [tag1]);

        Book book2 = CreateBook(
            title: bookTitleWithoutImage,
            description: "Description 2",
            publishedOn: fixedDate,
            publisher: "Publisher 2",
            price: bookPrice2,
            authors: [author2],
            tags: [tag2]);

        Book book3 = CreateBook(
            title: bookTitleSoftDeleted,
            description: "Description 3",
            publishedOn: fixedDate,
            publisher: "Publisher 3",
            price: bookPrice3,
            imageName: imageName3,
            softDeleted: true,
            authors: [author1]);

        await SeedAsync(author1, author2, tag1, tag2, book1, book2, book3);
        await UploadImageAsync(imageName1);

        // Act
        var response = await Client.GetAsync("/api/catalog/books", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PaginatedList<BookSummaryDto>>(TestContext.Current.CancellationToken);
        result.Should().NotBeNull();

        result.TotalCount.Should().Be(2);
        result.Items.Should().HaveCount(2);

        result.Items.Should().NotContain(b => b.Title == bookTitleSoftDeleted);

        // Book 1
        var bookWithImage = result.Items.Single(b => b.Title == bookTitleWithImage);
        bookWithImage.ImageUrl.Should().NotBeNullOrWhiteSpace();
        bookWithImage.ImageUrl.Should().Contain(CatalogTestConstants.BookCoversContainerName + "/" + imageName1);
        bookWithImage.Price.Should().Be(bookPrice1);

        // Book 2
        var bookWithoutImage = result.Items.Single(b => b.Title == bookTitleWithoutImage);
        bookWithoutImage.ImageUrl.Should().BeNull();
        bookWithoutImage.Price.Should().Be(bookPrice2);
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
                authors: [author1]),
            CreateBook(
                title: "Target Book 2",
                description: "Common description",
                publisher: "Common Publisher",
                authors: [author1]),
            CreateBook(
                title: "Irrelevant Book",
                authors: [author2])
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
        const string authorName1 = "Author 1";
        const string authorName2 = "Author 2";
        const string bookTitle1 = "Book by Author 1";
        const string bookTitle2 = "Book by Author 2";

        Author author1 = new() { Name = authorName1 };
        Author author2 = new() { Name = authorName2 };

        Book book1 = CreateBook(title: bookTitle1, authors: [author1]);
        Book book2 = CreateBook(title: bookTitle2, authors: [author2]);

        await SeedAsync(author1, author2, book1, book2);

        // Act
        var response = await Client.GetAsync($"/api/catalog/books?AuthorIds={author1.Id}", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PaginatedList<BookSummaryDto>>(TestContext.Current.CancellationToken);
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(1);
        result.Items.Should().ContainSingle(b => b.Title == bookTitle1);
    }

    [Fact]
    public async Task Search_WhenTagFilterApplied_ShouldReturnOnlyMatchingBooks()
    {
        // Arrange
        const string tagId1 = "Tag 1";
        const string tagId2 = "Tag 2";
        const string bookTitle1 = "Book with Tag 1";
        const string bookTitle2 = "Book with Tag 2";

        Tag tag1 = new() { Id = tagId1 };
        Tag tag2 = new() { Id = tagId2 };

        Book book1 = CreateBook(title: bookTitle1, tags: [tag1]);
        Book book2 = CreateBook(title: bookTitle2, tags: [tag2]);

        await SeedAsync(tag1, tag2, book1, book2);

        // Act
        var response = await Client.GetAsync($"/api/catalog/books?TagIds={tag1.Id}", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PaginatedList<BookSummaryDto>>(TestContext.Current.CancellationToken);
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(1);
        result.Items.Should().ContainSingle(b => b.Title == bookTitle1);
    }

    [Fact]
    public async Task Search_WhenMultipleAuthorsAndTagsApplied_ShouldReturnMatchingBooks()
    {
        // Arrange
        const string authorName1 = "Author 1";
        const string authorName2 = "Author 2";
        const string authorName3 = "Author 3";
        const string tagId1 = "Tag 1";
        const string tagId2 = "Tag 2";
        const string tagId3 = "Tag 3";
        const string bookTitle1 = "Book 1";
        const string bookTitle2 = "Book 2";
        const string bookTitle3 = "Book 3";

        Author author1 = new() { Name = authorName1 };
        Author author2 = new() { Name = authorName2 };
        Author author3 = new() { Name = authorName3 };

        Tag tag1 = new() { Id = tagId1 };
        Tag tag2 = new() { Id = tagId2 };
        Tag tag3 = new() { Id = tagId3 };

        Book book1 = CreateBook(title: bookTitle1, authors: [author1], tags: [tag1]);
        Book book2 = CreateBook(title: bookTitle2, authors: [author2], tags: [tag2]);
        Book book3 = CreateBook(title: bookTitle3, authors: [author3], tags: [tag3]);

        await SeedAsync(author1, author2, author3, tag1, tag2, tag3, book1, book2, book3);

        // Act
        var response = await Client.GetAsync($"/api/catalog/books?AuthorIds={author1.Id}&AuthorIds={author2.Id}&TagIds={tag1.Id}&TagIds={tag2.Id}", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PaginatedList<BookSummaryDto>>(TestContext.Current.CancellationToken);
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(2);
        result.Items.Should().HaveCount(2);
        result.Items.Should().Contain(b => b.Title == bookTitle1);
        result.Items.Should().Contain(b => b.Title == bookTitle2);
    }

    [Fact]
    public async Task Search_WhenAllFiltersApplied_ShouldReturnOnlyMatchingBooks()
    {
        // Arrange
        const string authorName1 = "Author 1";
        const string tagId1 = "Tag 1";
        const string bookTitleTarget = "Target Book";
        const string bookTitleWrongAuthor = "Target Book but wrong author";
        const string bookTitleWrongTag = "Target Book but wrong tag";

        Author author1 = new() { Name = authorName1 };
        Tag tag1 = new() { Id = tagId1 };

        Book book1 = CreateBook(title: bookTitleTarget, authors: [author1], tags: [tag1]);
        Book book2 = CreateBook(title: bookTitleWrongAuthor, tags: [tag1]);
        Book book3 = CreateBook(title: bookTitleWrongTag, authors: [author1]);

        await SeedAsync(author1, tag1, book1, book2, book3);

        // Act
        var response = await Client.GetAsync($"/api/catalog/books?SearchTerm=Target&AuthorIds={author1.Id}&TagIds={tag1.Id}", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<PaginatedList<BookSummaryDto>>(TestContext.Current.CancellationToken);
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(1);
        result.Items.Should().ContainSingle(b => b.Title == bookTitleTarget);
    }

    [Fact]
    public async Task Search_WhenPaginationApplied_ShouldReturnCorrectPage()
    {
        // Arrange
        const int totalBooks = 10;
        const int pageNumber = 2;
        const int pageSize = 3;
        const int expectedTotalPages = 4;
        const string bookTitlePrefix = "Book ";
        string[] expectedTitles = [$"{bookTitlePrefix}04", $"{bookTitlePrefix}05", $"{bookTitlePrefix}06"];

        Book[] books = [.. Enumerable.Range(1, totalBooks).Select(i => CreateBook(title: $"{bookTitlePrefix}{i:D2}"))];

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
        const decimal price1 = 20.00m;
        const decimal price2 = 10.00m;
        const decimal price3 = 30.00m;

        Book[] books =
        [
            CreateBook(title: "A Book", price: price1),
            CreateBook(title: "B Book", price: price2),
            CreateBook(title: "C Book", price: price3)
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
        const string authorName1 = "Author 1";
        const string authorName2 = "Author 2";
        const string tagName1 = "Tag 1";
        const string tagName2 = "Tag 2";
        const string bookTitle = "Calculated Data Book";
        const decimal bookPrice = 100.00m;
        const decimal promotionPrice = 80.00m;
        const string promotionText = "Sale!";
        const int reviewStars1 = 4;
        const int reviewStars2 = 5;
        const string reviewVoter1 = "Voter 1";
        const string reviewVoter2 = "Voter 2";
        const double expectedAverageRating = 4.5;
        const int expectedReviewCount = 2;

        Author author1 = new() { Name = authorName1 };
        Author author2 = new() { Name = authorName2 };
        Tag tag1 = new() { Id = tagName1 };
        Tag tag2 = new() { Id = tagName2 };

        Book book = CreateBook(
            title: bookTitle,
            price: bookPrice,
            authors: [author1, author2],
            tags: [tag1, tag2],
            promotion: new PriceOffer { NewPrice = promotionPrice, Text = promotionText },
            reviews:
            [
                new Review { Stars = reviewStars1, Comment = "Good", Voter = reviewVoter1 },
                new Review { Stars = reviewStars2, Comment = "Great", Voter = reviewVoter2 }
            ]);

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
        bookDto.Authors.Should().Contain(a => a.Name == authorName1);
        bookDto.Authors.Should().Contain(a => a.Name == authorName2);
        bookDto.Tags.Should().HaveCount(2);
        bookDto.Tags.Should().Contain(t => t.Id == tagName1);
        bookDto.Tags.Should().Contain(t => t.Id == tagName2);

        // Promotion
        bookDto.Promotion.Should().NotBeNull();
        bookDto.Promotion!.NewPrice.Should().Be(promotionPrice);
        bookDto.Promotion.Text.Should().Be(promotionText);

        // Reviews
        bookDto.ReviewCount.Should().Be(expectedReviewCount);
        bookDto.AverageRating.Should().Be(expectedAverageRating);
    }

    [Theory]
    [InlineData("PageNumber", 0)]
    [InlineData("PageNumber", -1)]
    [InlineData("PageSize", 0)]
    [InlineData("PageSize", IPagination.MaxPageSize + 1)]
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

    [Fact]
    public async Task Search_WhenSortByIsInvalid_ShouldReturnBadRequest()
    {
        // Act
        var response = await Client.GetAsync("/api/catalog/books?SortBy=InvalidProperty", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problemDetails = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestContext.Current.CancellationToken);
        problemDetails.Should().NotBeNull();
        problemDetails.Errors.Should().ContainKey("SortBy");
    }

    [Fact]
    public async Task Search_WhenMultipleParametersAreInvalid_ShouldReturnBadRequestWithAllErrors()
    {
        // Arrange
        const int invalidPageNumber = 0;
        const int invalidPageSize = -1;
        const int longSearchTermLength = 101;
        string longSearchTerm = new('a', longSearchTermLength);

        // Act
        var response = await Client.GetAsync($"/api/catalog/books?PageNumber={invalidPageNumber}&PageSize={invalidPageSize}&SearchTerm={longSearchTerm}", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problemDetails = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestContext.Current.CancellationToken);
        problemDetails.Should().NotBeNull();
        problemDetails.Errors.Should().HaveCount(3);
        problemDetails.Errors.Should().ContainKey("PageNumber");
        problemDetails.Errors.Should().ContainKey("PageSize");
        problemDetails.Errors.Should().ContainKey("Filter.SearchTerm");
    }
}