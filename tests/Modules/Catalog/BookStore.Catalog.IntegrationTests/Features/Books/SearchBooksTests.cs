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

        Book book1 = new()
        {
            Title = "Active Book With Image",
            Description = "Description 1",
            PublishedOn = fixedDate,
            Publisher = "Publisher 1",
            Price = 10.00m,
            ImageName = "image1.jpg",
            SoftDeleted = false,
            AuthorsLink = [new BookAuthor { Author = author1, Order = 1 }],
            Tags = [tag1]
        };

        Book book2 = new()
        {
            Title = "Active Book Without Image",
            Description = "Description 2",
            PublishedOn = fixedDate,
            Publisher = "Publisher 2",
            Price = 20.00m,
            ImageName = null,
            SoftDeleted = false,
            AuthorsLink = [new BookAuthor { Author = author2, Order = 1 }],
            Tags = [tag2]
        };

        Book book3 = new()
        {
            Title = "Soft Deleted Book",
            Description = "Description 3",
            PublishedOn = fixedDate,
            Publisher = "Publisher 3",
            Price = 30.00m,
            ImageName = "image3.jpg",
            SoftDeleted = true,
            AuthorsLink = [new BookAuthor { Author = author1, Order = 1 }]
        };

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
        bookWithImage.ImageUrl.Should().Contain("book-covers/image1.jpg");
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
            new()
            {
                Title = "Domain Driven Design",
                Description = "A great book",
                Publisher = "Publisher 1",
                Price = 50.00m
            },
            new()
            {
                Title = "Domain Analysis",
                Description = "Deep dive",
                Publisher = "Publisher 1",
                Price = 45.00m
            },
            new()
            {
                Title = "Clean Code",
                Description = "Essential reading",
                Publisher = "Publisher 2",
                Price = 40.00m
            }
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
        
        Book book1 = new()
        {
            Title = "Book by Author 1",
            Description = "Description",
            Publisher = "Publisher",
            Price = 10.00m,
            AuthorsLink = [new BookAuthor { Author = author1, Order = 1 }]
        };

        Book book2 = new()
        {
            Title = "Book by Author 2",
            Description = "Description",
            Publisher = "Publisher",
            Price = 20.00m,
            AuthorsLink = [new BookAuthor { Author = author2, Order = 1 }]
        };

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
        
        Book book1 = new()
        {
            Title = "Book with Tag 1",
            Description = "Description",
            Publisher = "Publisher",
            Price = 10.00m,
            Tags = [tag1]
        };

        Book book2 = new()
        {
            Title = "Book with Tag 2",
            Description = "Description",
            Publisher = "Publisher",
            Price = 20.00m,
            Tags = [tag2]
        };

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