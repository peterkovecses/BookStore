namespace BookStore.Common.UnitTests.Logging;

using Microsoft.Extensions.Logging;
using BookStore.Common.Logging;

public class LoggingBehaviorTests
{
    public sealed record TestRequest : IRequest<Result>;

    private readonly ILogger<LoggingBehavior<TestRequest, Result>> _logger = Substitute.For<ILogger<LoggingBehavior<TestRequest, Result>>>();
    private readonly LoggingBehavior<TestRequest, Result> _sut;

    public LoggingBehaviorTests()
    {
        _logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        _sut = new LoggingBehavior<TestRequest, Result>(_logger);
    }

    [Fact]
    public async Task Handle_WhenRequestSucceeds_ShouldLogStartingAndCompletion()
    {
        // Arrange
        var request = new TestRequest();
        var next = Substitute.For<RequestHandlerDelegate<Result>>();
        var expected = Result.Success();
        next().Returns(expected);

        // Act
        var actual = await _sut.Handle(request, next, CancellationToken.None);

        // Assert
        actual.Should().BeSuccess();
        
        _logger.Received(2).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Any<Arg.AnyType>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<Arg.AnyType, Exception?, string>>());
    }

    [Fact]
    public async Task Handle_WhenRequestFails_ShouldLogStartingFailureAndCompletion()
    {
        // Arrange
        var request = new TestRequest();
        var next = Substitute.For<RequestHandlerDelegate<Result>>();
        var error = Error.Failure("Test.Error", "Test message");
        var expected = Result.Failure(error);
        next().Returns(expected);

        // Act
        var actual = await _sut.Handle(request, next, CancellationToken.None);

        // Assert
        actual.Should().BeFailure();
        
        _logger.Received(2).Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Any<Arg.AnyType>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<Arg.AnyType, Exception?, string>>());

        _logger.Received(1).Log(
            LogLevel.Error,
            Arg.Any<EventId>(),
            Arg.Any<Arg.AnyType>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<Arg.AnyType, Exception?, string>>());
    }
}
