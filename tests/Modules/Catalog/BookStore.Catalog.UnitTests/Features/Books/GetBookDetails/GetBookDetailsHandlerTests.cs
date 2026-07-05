namespace BookStore.Catalog.UnitTests.Features.Books.GetBookDetails;

public class GetBookDetailsHandlerTests
{
    private readonly ICatalogDbContext _dbContext = Substitute.For<ICatalogDbContext>();
    private readonly ICatalogStorageService _storageService = Substitute.For<ICatalogStorageService>();
    private readonly GetBookDetailsHandler _sut;

    public GetBookDetailsHandlerTests()
    {
        _sut = new GetBookDetailsHandler(_dbContext, _storageService);
    }

    [Fact]
    public async Task HandleAsync_WhenBookExists_ShouldReturnSuccessWithDetails()
    {
        // Arrange
        var bookId = 1;
        var imageName = "cover.jpg";
        var expectedUrl = "https://cdn.com/cover.jpg";
        var book = new Book
        {
            Id = bookId,
            Title = "Test Book",
            Description = "Test Description",
            Publisher = "Test Publisher",
            PublishedOn = DateTime.UtcNow,
            Price = 10.00m,
            ImageName = imageName
        };

        var mockDbSet = new List<Book> { book }.BuildMockDbSet();
        _dbContext.Books.Returns(mockDbSet);
        _storageService.GetPublicUrl(imageName).Returns(expectedUrl);

        var request = new GetBookDetailsQuery(bookId);

        // Act
        var actual = await _sut.HandleAsync(request, CancellationToken.None);

        // Assert
        actual.Should().BeSuccess();
        actual.Should().HaveData(data =>
        {
            data.Id.Should().Be(bookId);
            data.Title.Should().Be(book.Title);
            data.Description.Should().Be(book.Description);
            data.ImageUrl.Should().Be(expectedUrl);
        });

        _storageService.Received(1).GetPublicUrl(imageName);
    }

    [Fact]
    public async Task HandleAsync_WhenBookHasImage_ShouldSetImageUrl()
    {
        // Arrange
        var bookId = 1;
        var imageName = "cover.jpg";
        var expectedUrl = "https://cdn.com/cover.jpg";
        var book = new Book
        {
            Id = bookId,
            Title = "Test Book",
            ImageName = imageName
        };

        var mockDbSet = new List<Book> { book }.BuildMockDbSet();
        _dbContext.Books.Returns(mockDbSet);
        _storageService.GetPublicUrl(imageName).Returns(expectedUrl);

        var request = new GetBookDetailsQuery(bookId);

        // Act
        var actual = await _sut.HandleAsync(request, CancellationToken.None);

        // Assert
        actual.Should().BeSuccess();
        actual.Should().HaveData(data => data.ImageUrl.Should().Be(expectedUrl));

        _storageService.Received(1).GetPublicUrl(imageName);
    }

    [Fact]
    public async Task HandleAsync_WhenBookHasNoImage_ShouldSetImageUrlToNull()
    {
        // Arrange
        var bookId = 1;
        var book = new Book
        {
            Id = bookId,
            Title = "Test Book",
            ImageName = null
        };

        var mockDbSet = new List<Book> { book }.BuildMockDbSet();
        _dbContext.Books.Returns(mockDbSet);

        var request = new GetBookDetailsQuery(bookId);

        // Act
        var actual = await _sut.HandleAsync(request, CancellationToken.None);

        // Assert
        actual.Should().BeSuccess();
        actual.Should().HaveData(data => data.ImageUrl.Should().BeNull());

        _storageService.DidNotReceiveWithAnyArgs().GetPublicUrl(default!);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task HandleAsync_WhenBookImageNameIsEmptyOrWhitespace_ShouldSetImageUrlToNull(string imageName)
    {
        // Arrange
        const int bookId = 1;
        const string title = "Test Book";
        var book = new Book
        {
            Id = bookId,
            Title = title,
            ImageName = imageName
        };

        var mockDbSet = new List<Book> { book }.BuildMockDbSet();
        _dbContext.Books.Returns(mockDbSet);

        var request = new GetBookDetailsQuery(bookId);

        // Act
        var actual = await _sut.HandleAsync(request, CancellationToken.None);

        // Assert
        actual.Should().BeSuccess();
        actual.Should().HaveData(data => data.ImageUrl.Should().BeNull());

        _storageService.DidNotReceiveWithAnyArgs().GetPublicUrl(default!);
    }

    [Fact]
    public async Task HandleAsync_WhenBookDoesNotExist_ShouldReturnNotFound()
    {
        // Arrange
        const int nonExistentBookId = 999;
        var mockDbSet = new List<Book>().BuildMockDbSet();
        _dbContext.Books.Returns(mockDbSet);

        var request = new GetBookDetailsQuery(nonExistentBookId);

        // Act
        var actual = await _sut.HandleAsync(request, CancellationToken.None);

        // Assert
        actual.Should().BeFailure();
        actual.Should().HaveError(ErrorCodes.NotFound);
    }

    [Fact]
    public async Task HandleAsync_WhenBookIsSoftDeleted_ShouldReturnNotFound()
    {
        // Arrange
        const int bookId = 1;
        var book = new Book
        {
            Id = bookId,
            Title = "Test Book",
            SoftDeleted = true
        };

        var mockDbSet = new List<Book> { book }.BuildMockDbSet();
        _dbContext.Books.Returns(mockDbSet);

        var request = new GetBookDetailsQuery(bookId);

        // Act
        var actual = await _sut.HandleAsync(request, CancellationToken.None);

        // Assert
        actual.Should().BeFailure();
        actual.Should().HaveError(ErrorCodes.NotFound);
    }
}
