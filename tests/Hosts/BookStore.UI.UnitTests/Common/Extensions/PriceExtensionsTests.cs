namespace BookStore.UI.UnitTests.Common.Extensions;

public class PriceExtensionsTests
{
    [Theory]
    [InlineData(10.00, "$10.00")]
    [InlineData(10.50, "$10.50")]
    [InlineData(0.00, "$0.00")]
    [InlineData(1234.56, "$1,234.56")]
    public void ToCurrency_ShouldReturnFormattedPriceInUsDollars(decimal price, string expected)
    {
        // Act
        var actual = price.ToCurrency();

        // Assert
        actual.Should().Be(expected);
    }
}
