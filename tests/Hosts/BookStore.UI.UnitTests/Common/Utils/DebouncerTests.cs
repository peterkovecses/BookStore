namespace BookStore.UI.UnitTests.Common.Utils;

public class DebouncerTests
{
    private readonly FakeTimeProvider _timeProvider = new();
    private readonly Debouncer _sut;

    public DebouncerTests()
    {
        _sut = new Debouncer(_timeProvider);
    }

    [Fact]
    public void Debounce_ShouldNotExecuteActionImmediately()
    {
        // Arrange
        var executed = false;
        
        // Act
        _sut.Debounce(() => executed = true, 100);

        // Assert
        executed.Should().BeFalse();
    }

    [Fact]
    public void Debounce_WhenTimeElapses_ShouldExecuteAction()
    {
        // Arrange
        var executed = false;
        _sut.Debounce(() => executed = true, 100);

        // Act
        _timeProvider.Advance(TimeSpan.FromMilliseconds(100));

        // Assert
        executed.Should().BeTrue();
    }

    [Fact]
    public void Debounce_WhenCalledMultipleTimes_ShouldResetTimer()
    {
        // Arrange
        var executionCount = 0;
        _sut.Debounce(() => executionCount++, 100);

        // Act
        _timeProvider.Advance(TimeSpan.FromMilliseconds(50));
        _sut.Debounce(() => executionCount++, 100); // Reset
        _timeProvider.Advance(TimeSpan.FromMilliseconds(60)); // Total 110 from start, but only 60 from reset

        // Assert
        executionCount.Should().Be(0);

        // Act again
        _timeProvider.Advance(TimeSpan.FromMilliseconds(40)); // Now total 100 from reset

        // Assert
        executionCount.Should().Be(1);
    }

    [Fact]
    public void Cancel_ShouldPreventActionExecution()
    {
        // Arrange
        var executed = false;
        _sut.Debounce(() => executed = true, 100);

        // Act
        _sut.Cancel();
        _timeProvider.Advance(TimeSpan.FromMilliseconds(100));

        // Assert
        executed.Should().BeFalse();
    }

    [Fact]
    public void Dispose_ShouldPreventActionExecution()
    {
        // Arrange
        var executed = false;
        _sut.Debounce(() => executed = true, 100);

        // Act
        _sut.Dispose();
        _timeProvider.Advance(TimeSpan.FromMilliseconds(100));

        // Assert
        executed.Should().BeFalse();
    }
}
