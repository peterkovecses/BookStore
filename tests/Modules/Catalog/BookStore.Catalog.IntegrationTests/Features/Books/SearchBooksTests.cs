namespace BookStore.Catalog.IntegrationTests.Features.Books;

public class SearchBooksTests(CatalogWebApplicationFactory factory) : CatalogIntegrationTestBase(factory)
{
    [Fact]
    public async Task Search_WhenNoFiltersApplied_ShouldReturnAllNonSoftDeletedBooks()
    {
        // Arrange
        var fixedDate = new DateTime(2026, 5, 6, 0, 0, 0, DateTimeKind.Utc);
        
        var authors = await ExecuteDbContextAsync(db => db.Authors.ToListAsync());
        var tags = await ExecuteDbContextAsync(db => db.Tags.ToListAsync());

        var books = new List<Book>
        {
            new()
            {
                Title = "Active Book With Image",
                Description = "Description 1",
                PublishedOn = fixedDate,
                Publisher = "Publisher 1",
                Price = 10.00m,
                ImageName = "image1.jpg",
                SoftDeleted = false,
                AuthorsLink = [new BookAuthor { AuthorId = authors[0].Id, Order = 1 }],
                Tags = [tags[0]]
            },
            new()
            {
                Title = "Active Book Without Image",
                Description = "Description 2",
                PublishedOn = fixedDate,
                Publisher = "Publisher 2",
                Price = 20.00m,
                ImageName = null,
                SoftDeleted = false,
                AuthorsLink = [new BookAuthor { AuthorId = authors[1].Id, Order = 1 }],
                Tags = [tags[1]]
            },
            new()
            {
                Title = "Soft Deleted Book",
                Description = "Description 3",
                PublishedOn = fixedDate,
                Publisher = "Publisher 3",
                Price = 30.00m,
                ImageName = "image3.jpg",
                SoftDeleted = true,
                AuthorsLink = [new BookAuthor { AuthorId = authors[0].Id, Order = 1 }]
            }
        };

        await SeedBooksAsync(books);
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
        var books = new List<Book>
        {
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
        };

        await SeedBooksAsync(books);

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
        var authors = await ExecuteDbContextAsync(db => db.Authors.ToListAsync());
        
        var books = new List<Book>
        {
            new()
            {
                Title = "Book by Author 1",
                Description = "Description",
                Publisher = "Publisher",
                Price = 10.00m,
                AuthorsLink = [new BookAuthor { AuthorId = authors[0].Id, Order = 1 }]
            },
            new()
            {
                Title = "Book by Author 2",
                Description = "Description",
                Publisher = "Publisher",
                Price = 20.00m,
                AuthorsLink = [new BookAuthor { AuthorId = authors[1].Id, Order = 1 }]
            }
        };

        await SeedBooksAsync(books);

        // Act
        var response = await Client.GetAsync($"/api/catalog/books?AuthorIds={authors[0].Id}", TestContext.Current.CancellationToken);

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
        var tags = await ExecuteDbContextAsync(db => db.Tags.ToListAsync());
        
        var books = new List<Book>
        {
            new()
            {
                Title = "Book with Tag 1",
                Description = "Description",
                Publisher = "Publisher",
                Price = 10.00m,
                Tags = [tags[0]]
            },
            new()
            {
                Title = "Book with Tag 2",
                Description = "Description",
                Publisher = "Publisher",
                Price = 20.00m,
                Tags = [tags[1]]
            }
        };

        await SeedBooksAsync(books);

        // Act
        var response = await Client.GetAsync($"/api/catalog/books?TagIds={tags[0].Id}", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var result = await response.Content.ReadFromJsonAsync<PaginatedList<BookSummaryDto>>(TestContext.Current.CancellationToken);
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(1);
        result.Items.Should().ContainSingle(b => b.Title == "Book with Tag 1");
    }
}