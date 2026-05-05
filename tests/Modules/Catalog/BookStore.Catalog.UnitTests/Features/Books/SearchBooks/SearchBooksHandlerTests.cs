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
    public async Task Handle_WhenNoBooks_ShouldReturnEmptyPaginatedList()
    {
        // Arrange
        var books = new List<Book>();
        var mockDbSet = books.BuildMockDbSet();
        _dbContext.Books.Returns(mockDbSet);
        var request = new SearchBooksQuery(new SearchBooksFilter(), 1, 10, null, false);

        // Act
        var actual = await _sut.Handle(request, CancellationToken.None);

        // Assert
        actual.Should().HaveData(list => list.Items.Should().BeEmpty());

        _storageService.DidNotReceiveWithAnyArgs().GetPublicUrl(default!);
    }

    [Fact]
    public async Task Handle_WhenBooksHaveImages_ShouldPopulateImageUrls()
    {
        // Arrange
        var imageName = "cover.jpg";
        var expectedUrl = "https://cdn.com/cover.jpg";
        var books = new List<Book>
        {
            new() { Id = 1, Title = "Book 1", ImageName = imageName }
        };
        var mockDbSet = books.BuildMockDbSet();
        _dbContext.Books.Returns(mockDbSet);
        
        _storageService.GetPublicUrl(imageName).Returns(expectedUrl);
        
        var request = new SearchBooksQuery(new SearchBooksFilter(), 1, 10, null, false);

        // Act
        var actual = await _sut.Handle(request, CancellationToken.None);

        // Assert
        actual.Should().HaveData(list => list.Items.Should().AllSatisfy(i => i.ImageUrl.Should().Be(expectedUrl)));
    
        _storageService.Received(1).GetPublicUrl(imageName);
    }

    [Fact]
    public async Task Handle_WhenBooksHaveNoImages_ShouldNotCallStorageService()
    {
        // Arrange
        var books = new List<Book>
        {
            new() { Id = 1, Title = "Book 1", ImageName = null }
        };
        var mockDbSet = books.BuildMockDbSet();
        _dbContext.Books.Returns(mockDbSet);
        
        var request = new SearchBooksQuery(new SearchBooksFilter(), 1, 10, null, false);

        // Act
        var actual = await _sut.Handle(request, CancellationToken.None);

        // Assert
        actual.Should().WhichData.Items.Should().AllSatisfy(i => i.ImageUrl.Should().BeNull());
        _storageService.DidNotReceiveWithAnyArgs().GetPublicUrl(default!);
    }
}
