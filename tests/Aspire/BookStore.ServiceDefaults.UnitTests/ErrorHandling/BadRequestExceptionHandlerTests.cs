namespace BookStore.ServiceDefaults.UnitTests.ErrorHandling;

public class BadRequestExceptionHandlerTests
{
    private readonly ILogger<BadRequestExceptionHandler> _logger = Substitute.For<ILogger<BadRequestExceptionHandler>>();
    private readonly IProblemDetailsService _problemDetailsService = Substitute.For<IProblemDetailsService>();
    private readonly BadRequestExceptionHandler _sut;

    public BadRequestExceptionHandlerTests()
    {
        _sut = new BadRequestExceptionHandler(_logger, _problemDetailsService);
    }

    [Fact]
    public async Task TryHandleAsync_WhenExceptionIsNotBadRequest_ShouldReturnFalse()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var exception = new Exception("Regular exception");

        // Act
        var actual = await _sut.TryHandleAsync(context, exception, TestContext.Current.CancellationToken);

        // Assert
        actual.Should().BeFalse();
    }

    [Fact]
    public async Task TryHandleAsync_WhenExceptionIsBadRequest_ShouldReturnTrueAndWriteProblemDetails()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var exception = new BadHttpRequestException("Bad request");

        _problemDetailsService.TryWriteAsync(Arg.Any<ProblemDetailsContext>())
            .Returns(true);

        // Act
        var actual = await _sut.TryHandleAsync(context, exception, TestContext.Current.CancellationToken);

        // Assert
        actual.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        
        await _problemDetailsService.Received(1).TryWriteAsync(Arg.Is<ProblemDetailsContext>(problemContext => 
            problemContext.HttpContext == context &&
            problemContext.Exception == exception &&
            problemContext.ProblemDetails.Status == StatusCodes.Status400BadRequest &&
            problemContext.ProblemDetails.Title == "Bad Request" &&
            problemContext.ProblemDetails.Detail == "Bad request"));
    }
}
