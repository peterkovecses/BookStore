namespace BookStore.ServiceDefaults.UnitTests.ErrorHandling;

public class GlobalExceptionHandlerTests
{
    private readonly ILogger<GlobalExceptionHandler> _logger = Substitute.For<ILogger<GlobalExceptionHandler>>();
    private readonly IHostEnvironment _environment = Substitute.For<IHostEnvironment>();
    private readonly GlobalExceptionHandler _sut;

    public GlobalExceptionHandlerTests()
    {
        _sut = new GlobalExceptionHandler(_logger, _environment);
    }

    [Fact]
    public async Task TryHandleAsync_WhenEnvironmentIsDevelopment_ShouldReturnTrueAndDetailedProblemDetails()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var exception = new Exception("Test exception");
        _environment.EnvironmentName.Returns(Environments.Development);

        // Act
        var actual = await _sut.TryHandleAsync(context, exception, TestContext.Current.CancellationToken);

        // Assert
        actual.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);

        context.Response.Body.Position = 0;
        var problemDetails = await JsonSerializer.DeserializeAsync<ProblemDetails>(context.Response.Body, cancellationToken: TestContext.Current.CancellationToken);
        problemDetails.Should().NotBeNull();
        problemDetails!.Detail.Should().Be(exception.Message);
        problemDetails.Extensions.Should().ContainKey("exception");
    }

    [Theory]
    [InlineData("Testing")]
    [InlineData("Staging")]
    [InlineData("Production")]
    public async Task TryHandleAsync_WhenEnvironmentIsNotDevelopment_ShouldReturnTrueAndGenericProblemDetails(string environmentName)
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var exception = new Exception("Test exception");
        _environment.EnvironmentName.Returns(environmentName);

        // Act
        var actual = await _sut.TryHandleAsync(context, exception, TestContext.Current.CancellationToken);

        // Assert
        actual.Should().BeTrue();
        context.Response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);

        context.Response.Body.Position = 0;
        var problemDetails = await JsonSerializer.DeserializeAsync<ProblemDetails>(context.Response.Body, cancellationToken: TestContext.Current.CancellationToken);
        problemDetails.Should().NotBeNull();
        problemDetails.Detail.Should().NotBe(exception.Message);
        problemDetails.Extensions.Should().NotContainKey("exception");
    }
}
