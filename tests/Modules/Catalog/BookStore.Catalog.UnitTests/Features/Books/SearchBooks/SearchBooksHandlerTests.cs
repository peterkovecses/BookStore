namespace BookStore.Catalog.UnitTests.Features.Books.SearchBooks;

public class SearchBooksHandlerTests
{
    private readonly ICatalogDbContext _dbContext = Substitute.For<ICatalogDbContext>();
    private readonly ICatalogStorageService _storageService = Substitute.For<ICatalogStorageService>();
    private readonly SearchBooksHandler _sut;

    public SearchBooksHandlerTests()
    {
        _sut = new SearchBooksHandler(_dbContext, _storageService);
    }

    [Fact]
    public async Task Handle_WhenNoBooks_ShouldReturnEmptyList()
    {
        // Arrange
        const int pageNumber = 1;
        const int pageSize = 10;
        var mockDbSet = new List<Book>().BuildMockDbSet();
        _dbContext.Books.Returns(mockDbSet);
        var request = new SearchBooksQuery(new SearchBooksFilter(), IPagination.Create(pageNumber, pageSize), ISorting.Create(null, false));

        // Act
        var actual = await _sut.HandleAsync(request, CancellationToken.None);

        // Assert
        actual.ShouldBePaginatedSuccess()
            .HaveCount(0);

        _storageService.DidNotReceiveWithAnyArgs().GetPublicUrl(default!);
    }

    [Fact]
    public async Task Handle_WhenMultipleBooksHaveImages_ShouldPopulateImageUrls()
    {
        // Arrange
        const int bookId1 = 1;
        const int bookId2 = 2;
        const int pageNumber = 1;
        const int pageSize = 10;
        var imageName1 = "cover1.jpg";
        var expectedUrl1 = "https://cdn.com/cover1.jpg";
        var imageName2 = "cover2.jpg";
        var expectedUrl2 = "https://cdn.com/cover2.jpg";
        
        var books = new List<Book>
        {
            new() { Id = bookId1, Title = "Book 1", ImageName = imageName1 },
            new() { Id = bookId2, Title = "Book 2", ImageName = imageName2 }
        };
        var mockDbSet = books.BuildMockDbSet();
        _dbContext.Books.Returns(mockDbSet);
        
        _storageService.GetPublicUrl(imageName1).Returns(expectedUrl1);
        _storageService.GetPublicUrl(imageName2).Returns(expectedUrl2);
        
        var request = new SearchBooksQuery(new SearchBooksFilter(), IPagination.Create(pageNumber, pageSize), ISorting.Create(null, false));

        // Act
        var actual = await _sut.HandleAsync(request, CancellationToken.None);

        // Assert
        actual.ShouldBePaginatedSuccess()
            .SatisfyRespectively(
                first => first.ImageUrl.Should().Be(expectedUrl1),
                second => second.ImageUrl.Should().Be(expectedUrl2)
            );
    
        _storageService.Received(1).GetPublicUrl(imageName1);
        _storageService.Received(1).GetPublicUrl(imageName2);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_WhenBookHasNoImage_ShouldNotCallStorageService(string? imageName)
    {
        // Arrange
        const int bookId = 1;
        const string title = "Book 1";
        const int pageNumber = 1;
        const int pageSize = 10;
        const string? sortBy = null;
        const bool isDescending = false;
        var mockDbSet = new List<Book> { new() { Id = bookId, Title = title, ImageName = imageName } }.BuildMockDbSet();
        _dbContext.Books.Returns(mockDbSet);
        
        var request = new SearchBooksQuery(new SearchBooksFilter(), IPagination.Create(pageNumber, pageSize), ISorting.Create(sortBy, isDescending));

        // Act
        var actual = await _sut.HandleAsync(request, CancellationToken.None);

        // Assert
        actual.ShouldBePaginatedSuccess()
            .AllSatisfy(i => i.ImageUrl.Should().BeNull());

        _storageService.DidNotReceiveWithAnyArgs().GetPublicUrl(default!);
    }

    [Fact]
    public async Task Handle_WhenFilteredBySearchTerm_ShouldReturnMatchingBooks()
    {
        // Arrange
        const int pageNumber = 1;
        const int pageSize = 10;
        var searchTerm = "Domain";
        var books = new List<Book>
        {
            new() { Id = 1, Title = "Domain Driven Design", Description = "", Publisher = "" },
            new() { Id = 2, Title = "Clean Code", Description = "", Publisher = "" },
            new() { Id = 3, Title = "Other", Description = "Learning Domain logic", Publisher = "" },
            new() { Id = 4, Title = "Expert", Description = "", Publisher = "", AuthorsLink = [new() { Author = new() { Name = "Domain Expert" } }] }
        };
        var mockDbSet = books.BuildMockDbSet();
        _dbContext.Books.Returns(mockDbSet);
        
        var request = new SearchBooksQuery(new SearchBooksFilter { SearchTerm = searchTerm }, IPagination.Create(pageNumber, pageSize), ISorting.Create(null, false));

        // Act
        var actual = await _sut.HandleAsync(request, CancellationToken.None);

        // Assert
        actual.ShouldBePaginatedSuccess()
            .HaveCount(3)
            .SatisfyRespectively(
                b => b.Title.Should().Be("Domain Driven Design"),
                b => b.Title.Should().Be("Other"),
                b => b.Authors.Should().Contain(a => a.Name == "Domain Expert")
            );
    }

    [Fact]
    public async Task Handle_WhenFilteredByAuthorIds_ShouldReturnMatchingBooks()
    {
        // Arrange
        const int authorId = 1;
        const int otherAuthorId = 2;
        const int pageNumber = 1;
        const int pageSize = 10;
        var books = new List<Book>
        {
            new() { Id = 1, Title = "Book 1", AuthorsLink = [new() { AuthorId = authorId, Author = new() { Name = "A1" } }] },
            new() { Id = 2, Title = "Book 2", AuthorsLink = [new() { AuthorId = otherAuthorId, Author = new() { Name = "A2" } }] }
        };
        var mockDbSet = books.BuildMockDbSet();
        _dbContext.Books.Returns(mockDbSet);
        
        var request = new SearchBooksQuery(new SearchBooksFilter { AuthorIds = [authorId] }, IPagination.Create(pageNumber, pageSize), ISorting.Create(null, false));

        // Act
        var actual = await _sut.HandleAsync(request, CancellationToken.None);

        // Assert
        actual.ShouldBePaginatedSuccess()
            .HaveCount(1)
            .AllSatisfy(b => b.Id.Should().Be(1));
    }

    [Fact]
    public async Task Handle_WhenFilteredByTagIds_ShouldReturnMatchingBooks()
    {
        // Arrange
        const int pageNumber = 1;
        const int pageSize = 10;
        var tagId = "tag-1";
        var otherTagId = "tag-2";
        var books = new List<Book>
        {
            new() { Id = 1, Title = "Book 1", Tags = [new() { Id = tagId }], AuthorsLink = [], Reviews = [] },
            new() { Id = 2, Title = "Book 2", Tags = [new() { Id = otherTagId }], AuthorsLink = [], Reviews = [] }
        };
        var mockDbSet = books.BuildMockDbSet();
        _dbContext.Books.Returns(mockDbSet);
        
        var request = new SearchBooksQuery(new SearchBooksFilter { TagIds = [tagId] }, IPagination.Create(pageNumber, pageSize), ISorting.Create(null, false));

        // Act
        var actual = await _sut.HandleAsync(request, CancellationToken.None);

        // Assert
        actual.ShouldBePaginatedSuccess()
            .HaveCount(1)
            .AllSatisfy(b => b.Id.Should().Be(1));
    }

    [Fact]
    public async Task Handle_WhenSortedByTitleDescending_ShouldSortCorrectly()
    {
        // Arrange
        const int pageNumber = 1;
        const int pageSize = 10;
        const string sortBy = "Title";
        var books = new List<Book>
        {
            new() { Id = 1, Title = "A" },
            new() { Id = 2, Title = "C" },
            new() { Id = 3, Title = "B" }
        };
        var mockDbSet = books.BuildMockDbSet();
        _dbContext.Books.Returns(mockDbSet);
        
        var request = new SearchBooksQuery(new SearchBooksFilter(), IPagination.Create(pageNumber, pageSize), ISorting.Create(sortBy, true));

        // Act
        var actual = await _sut.HandleAsync(request, CancellationToken.None);

        // Assert
        actual.ShouldBePaginatedSuccess()
            .HaveCount(3)
            .SatisfyRespectively(
                b => b.Title.Should().Be("C"),
                b => b.Title.Should().Be("B"),
                b => b.Title.Should().Be("A")
            );
    }

    [Fact]
    public async Task Handle_WhenSecondPageRequested_ShouldReturnCorrectPage()
    {
        // Arrange
        const int totalBooks = 15;
        const int pageNumber = 2;
        const int pageSize = 5;
        const string sortBy = "Title";
        var books = Enumerable.Range(1, totalBooks)
            .Select(i => new Book { Id = i, Title = $"Book {i:D2}" })
            .ToList();
        var mockDbSet = books.BuildMockDbSet();
        _dbContext.Books.Returns(mockDbSet);
        
        var request = new SearchBooksQuery(new SearchBooksFilter(), IPagination.Create(pageNumber, pageSize), ISorting.Create(sortBy, false));

        // Act
        var actual = await _sut.HandleAsync(request, CancellationToken.None);

        // Assert
        actual.ShouldBePaginatedSuccess()
            .HavePageNumber(pageNumber)
            .HaveCount(pageSize)
            .HaveTotalCount(totalBooks)
            .SatisfyRespectively(
                b => b.Title.Should().Be("Book 06"),
                b => b.Title.Should().Be("Book 07"),
                b => b.Title.Should().Be("Book 08"),
                b => b.Title.Should().Be("Book 09"),
                b => b.Title.Should().Be("Book 10")
            );
    }
}
