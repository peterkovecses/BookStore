namespace BookStore.UI.UnitTests.Common.Models;

public class PaginationParametersTests
{
    private readonly PaginationParameters _sut = new();

    [Fact]
    public void Reset_ShouldSetPageNumberToMinPageNumber()
    {
        // Arrange
        _sut.PageNumber = 5;

        // Act
        _sut.Reset();

        // Assert
        _sut.PageNumber.Should().Be(IPagination.MinPageNumber);
    }
}
