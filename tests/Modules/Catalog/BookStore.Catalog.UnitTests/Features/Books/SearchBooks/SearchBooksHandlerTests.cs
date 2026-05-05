namespace BookStore.Catalog.UnitTests.Features.Books.SearchBooks;

public class SearchBooksHandlerTests
{
    private readonly ICatalogDbContext _dbContext = Substitute.For<ICatalogDbContext>();
    private readonly BlobServiceClient _blobServiceClient = Substitute.For<BlobServiceClient>();
    private readonly SearchBooksHandler _sut;

    public SearchBooksHandlerTests()
    {
        _sut = new SearchBooksHandler(_dbContext, _blobServiceClient);
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
        actual.Should().BeSuccess()
            .HaveData(list => list is not null && list.Items.Count == 0);
    }
}