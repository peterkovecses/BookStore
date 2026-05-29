namespace BookStore.UI.UnitTests.Common.Extensions;

public class HttpResponseMessageExtensionsTests
{
    [Fact]
    public async Task ToResultAsync_WhenSuccessAndValidData_ShouldReturnSuccessWithData()
    {
        // Arrange
        var expectedData = new TestData { Id = 1, Name = "Test" };
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(expectedData)
        };

        // Act
        var actual = await response.ToResultAsync<TestData>(CancellationToken.None);

        // Assert
        actual.Should().BeSuccess()
            .HaveData(x => x.Should().BeEquivalentTo(expectedData));
    }

    [Fact]
    public async Task ToResultAsync_WhenSuccessAndDataIsNull_ShouldReturnFailureWithEmptyResponse()
    {
        // Arrange
        using var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("null", Encoding.UTF8, "application/json")
        };

        // Act
        var actual = await response.ToResultAsync<TestData>(CancellationToken.None);

        // Assert
        actual.Should().BeFailure()
            .HaveError(UIErrorCodes.EmptyResponse);
    }

    [Fact]
    public async Task ToResultAsync_WhenBadRequestWithValidationErrors_ShouldReturnFailureWithValidationMetadata()
    {
        // Arrange
        var validationProblem = new ValidationProblemDetails
        {
            Errors = new Dictionary<string, string[]> { { "Property", ["Error"] } }
        };
        using var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = JsonContent.Create(validationProblem)
        };

        // Act
        var actual = await response.ToResultAsync<TestData>(CancellationToken.None);

        // Assert
        actual.Should().BeFailure()
            .HaveError(UIErrorCodes.Validation)
            .HaveValidationProperty("Property")
            .Contain("Error");
    }

    [Fact]
    public async Task ToResultAsync_WhenBadRequestWithNullErrors_ShouldFallbackToProblemDetails()
    {
        // Arrange
        const string expectedMessage = "Detailed error message";
        var validationProblem = new ValidationProblemDetails
        {
            Detail = expectedMessage,
            Errors = null!
        };
        using var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = JsonContent.Create(validationProblem)
        };

        // Act
        var actual = await response.ToResultAsync<TestData>(CancellationToken.None);

        // Assert
        actual.Should().BeFailure()
            .HaveError(UIErrorCodes.ApiError)
            .HaveMessage(expectedMessage);
    }

    [Fact]
    public async Task ToResultAsync_WhenBadRequestWithInvalidJson_ShouldReturnFailureWithApiError()
    {
        // Arrange
        using var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("invalid-json", Encoding.UTF8, "application/json")
        };

        // Act
        var actual = await response.ToResultAsync<TestData>(CancellationToken.None);

        // Assert
        actual.Should().BeFailure()
            .HaveError(UIErrorCodes.ApiError)
            .HaveMessage("API call failed with status code BadRequest");
    }

    [Fact]
    public async Task ToResultAsync_WhenErrorWithProblemDetailsDetail_ShouldReturnFailureWithDetailMessage()
    {
        // Arrange
        const string detail = "Specific error detail";
        var problem = new ProblemDetails { Detail = detail };
        using var response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = JsonContent.Create(problem)
        };

        // Act
        var actual = await response.ToResultAsync<TestData>(CancellationToken.None);

        // Assert
        actual.Should().BeFailure()
            .HaveError(UIErrorCodes.ApiError)
            .HaveMessage(detail);
    }

    [Fact]
    public async Task ToResultAsync_WhenErrorWithProblemDetailsTitleOnly_ShouldReturnFailureWithTitleMessage()
    {
        // Arrange
        const string title = "Error Title";
        var problem = new ProblemDetails { Title = title };
        using var response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = JsonContent.Create(problem)
        };

        // Act
        var actual = await response.ToResultAsync<TestData>(CancellationToken.None);

        // Assert
        actual.Should().BeFailure()
            .HaveError(UIErrorCodes.ApiError)
            .HaveMessage(title);
    }

    [Fact]
    public async Task ToResultAsync_WhenErrorWithInvalidJson_ShouldReturnFailureWithStatusCodeMessage()
    {
        // Arrange
        using var response = new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent("not-found", Encoding.UTF8, "text/plain")
        };

        // Act
        var actual = await response.ToResultAsync<TestData>(CancellationToken.None);

        // Assert
        actual.Should().BeFailure()
            .HaveError(UIErrorCodes.ApiError)
            .HaveMessage("API call failed with status code NotFound");
    }

    [Fact]
    public async Task ToResultAsync_WhenExceptionOccurs_ShouldReturnFailureWithExceptionError()
    {
        // Arrange
        using var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Dispose(); // This will cause an ObjectDisposedException when reading content

        // Act
        var actual = await response.ToResultAsync<TestData>(CancellationToken.None);

        // Assert
        actual.Should().BeFailure()
            .HaveError(UIErrorCodes.Exception);
    }

    private class TestData
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
