namespace BookStore.Common.UnitTests.Pagination;

public class PaginatedListTests
{
    private const int DefaultPageSize = 5;
    private const int DefaultTotalCount = 10;
    private const int DefaultCurrentPage = 1;

    [Theory]
    [InlineData(10, 5, 2)]
    [InlineData(11, 5, 3)]
    [InlineData(0, 5, 0)]
    [InlineData(5, 10, 1)]
    [InlineData(1, 5, 1)]
    [InlineData(25, 5, 5)]
    [InlineData(1000, 7, 143)]
    public void TotalPages_ShouldReturnExpectedValue(int totalCount, int pageSize, int expectedTotalPages)
    {
        // Arrange
        var sut = new PaginatedList<int>([], totalCount, DefaultCurrentPage, pageSize);

        // Act
        var actual = sut.TotalPages;

        // Assert
        actual.Should().Be(expectedTotalPages);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    [InlineData(5, true)]
    public void HasPreviousPage_ShouldReturnExpectedValue(int pageNumber, bool expectedHasPreviousPage)
    {
        // Arrange
        var sut = new PaginatedList<int>([], DefaultTotalCount, pageNumber, DefaultPageSize);

        // Act
        var actual = sut.HasPreviousPage;

        // Assert
        actual.Should().Be(expectedHasPreviousPage);
    }

    [Theory]
    [InlineData(1, 2, true)]
    [InlineData(2, 2, false)]
    [InlineData(3, 2, false)]
    [InlineData(1, 5, true)]
    [InlineData(4, 5, true)]
    [InlineData(5, 5, false)]
    public void HasNextPage_ShouldReturnExpectedValue(int pageNumber, int totalPages, bool expectedHasNextPage)
    {
        // Arrange
        var totalCount = totalPages * DefaultPageSize;
        var sut = new PaginatedList<int>([], totalCount, pageNumber, DefaultPageSize);

        // Act
        var actual = sut.HasNextPage;

        // Assert
        actual.Should().Be(expectedHasNextPage);
    }


    [Fact]
    public void HasNextPage_WhenTotalCountIsZero_ShouldBeFalse()
    {
        // Arrange
        const int totalCount = 0;
        const int pageNumber = 1;
        const int pageSize = 5;
        var sut = new PaginatedList<int>([], totalCount, pageNumber, pageSize);

        // Act
        var actual = sut.HasNextPage;

        // Assert
        actual.Should().BeFalse();
    }
}