namespace BookStore.UI.UnitTests.Common.Models;

public class SortingParametersTests
{
    private readonly SortingParameters _sut = new();

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void ToggleOrder_ShouldFlipIsDescending(bool initial, bool expected)
    {
        // Arrange
        _sut.IsDescending = initial;

        // Act
        _sut.ToggleOrder();

        // Assert
        _sut.IsDescending.Should().Be(expected);
    }
}
