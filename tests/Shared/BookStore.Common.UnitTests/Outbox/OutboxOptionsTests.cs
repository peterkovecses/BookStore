namespace BookStore.Common.UnitTests.Outbox;

public class OutboxOptionsTests
{
    [Fact]
    public void IsValid_WhenDefaultsAreUsed_ShouldReturnTrue()
    {
        // Arrange
        var sut = new OutboxOptions();

        // Act
        var actual = sut.IsValid();

        // Assert
        actual.Should().BeTrue();
    }

    [Fact]
    public void IsValid_WhenTimeoutIsNotShorterThanLease_ShouldReturnFalse()
    {
        // Arrange
        var sut = new OutboxOptions { HandlerTimeout = TimeSpan.FromMinutes(5) };

        // Act
        var actual = sut.IsValid();

        // Assert
        actual.Should().BeFalse();
    }

    [Fact]
    public void IsValid_WhenRenewalIsNotShorterThanLease_ShouldReturnFalse()
    {
        // Arrange
        var sut = new OutboxOptions { RenewalInterval = TimeSpan.FromMinutes(5) };

        // Act
        var actual = sut.IsValid();

        // Assert
        actual.Should().BeFalse();
    }
}
