namespace BookStore.UI.UnitTests.Common.Clients;

public class BookStoreApiClientTests
{
    private readonly MockHttpMessageHandler _httpMessageHandler = new();
    private readonly HttpClient _httpClient;
    private readonly IJSRuntime _jsRuntime = Substitute.For<IJSRuntime>();
    private readonly IConfiguration _configuration = Substitute.For<IConfiguration>();
    private readonly BookStoreApiClient _sut;

    public BookStoreApiClientTests()
    {
        _httpClient = new HttpClient(_httpMessageHandler) { BaseAddress = new Uri("http://localhost/") };
        
        _jsRuntime.InvokeAsync<string>("getApiUrl", Arg.Any<object?[]>())
            .Returns(new ValueTask<string>("http://localhost/"));

        _sut = new BookStoreApiClient(_httpClient, _jsRuntime, _configuration);
    }

    [Fact]
    public async Task SearchBooksAsync_WhenSuccessful_ShouldReturnSuccessWithData()
    {
        // Arrange
        var expected = new PaginatedList<BookSummaryDto>([], 0, 1, 10);
        _httpMessageHandler.SetResponse(HttpStatusCode.OK, expected);

        // Act
        var actual = await _sut.SearchBooksAsync(new BookSearchParameters(), new PaginationParameters(), new SortingParameters(), CancellationToken.None);

        // Assert
        actual.Should().BeSuccess()
            .HaveData(x => x.Should().BeEquivalentTo(expected));
    }

    [Fact]
    public async Task SearchBooksAsync_WithMultipleCollectionItems_ShouldGenerateCorrectQueryString()
    {
        // Arrange
        var searchParams = new BookSearchParameters
        {
            AuthorIds = [1, 2],
            TagIds = ["tag1", "tag2"]
        };
        _httpMessageHandler.SetResponse(HttpStatusCode.OK, new PaginatedList<BookSummaryDto>([], 0, 1, 10));

        // Act
        await _sut.SearchBooksAsync(searchParams, new PaginationParameters(), new SortingParameters(), CancellationToken.None);

        // Assert
        var lastRequest = _httpMessageHandler.LastRequest;
        lastRequest.Should().NotBeNull();
        lastRequest!.RequestUri!.Query.Should().Contain("authorIds=1");
        lastRequest.RequestUri.Query.Should().Contain("authorIds=2");
        lastRequest.RequestUri.Query.Should().Contain("tagIds=tag1");
        lastRequest.RequestUri.Query.Should().Contain("tagIds=tag2");
    }

    [Fact]
    public async Task SearchBooksAsync_WhenBadRequestWithValidationErrors_ShouldReturnFailureWithValidationMetadata()
    {
        // Arrange
        var validationProblem = new ValidationProblemDetails
        {
            Errors = new Dictionary<string, string[]> { { "PageNumber", ["Must be greater than 0"] } }
        };
        _httpMessageHandler.SetResponse(HttpStatusCode.BadRequest, validationProblem);

        // Act
        var actual = await _sut.SearchBooksAsync(new BookSearchParameters(), new PaginationParameters(), new SortingParameters(), CancellationToken.None);

        // Assert
        actual.Should().BeFailure()
            .HaveError(UIErrorCodes.Validation)
                .HaveValidationProperty("PageNumber")
                    .Contain("Must be greater than 0");
    }

    [Fact]
    public async Task SearchBooksAsync_WhenApiReturnsProblemDetails_ShouldReturnFailureWithErrorMessage()
    {
        // Arrange
        const string expectedDetail = "Some API error";
        var problem = new ProblemDetails { Detail = expectedDetail };
        _httpMessageHandler.SetResponse(HttpStatusCode.InternalServerError, problem);

        // Act
        var actual = await _sut.SearchBooksAsync(new BookSearchParameters(), new PaginationParameters(), new SortingParameters(), CancellationToken.None);

        // Assert
        actual.Should().BeFailure()
            .HaveError(UIErrorCodes.ApiError)
                .HaveMessage(expectedDetail);
    }

    [Fact]
    public async Task GetBookDetailsAsync_WhenCalled_ShouldReturnSuccessWithData()
    {
        // Arrange
        const int id = 1;
        var expected = new BookDetailsDto { Id = id, Title = "Test Book" };
        _httpMessageHandler.SetResponse(HttpStatusCode.OK, expected);

        // Act
        var actual = await _sut.GetBookDetailsAsync(id, CancellationToken.None);

        // Assert
        actual.Should().BeSuccess()
            .HaveData(x => x.Should().BeEquivalentTo(expected));
        _httpMessageHandler.LastRequest?.RequestUri?.AbsolutePath.Should().Be($"/api/catalog/books/{id}");
    }

    [Fact]
    public async Task GetSearchFiltersAsync_WhenCalled_ShouldReturnSuccessWithData()
    {
        // Arrange
        var expected = new BookSearchFiltersDto { Authors = [], TagIds = [] };
        _httpMessageHandler.SetResponse(HttpStatusCode.OK, expected);

        // Act
        var actual = await _sut.GetSearchFiltersAsync(CancellationToken.None);

        // Assert
        actual.Should().BeSuccess()
            .HaveData(x => x.Should().BeEquivalentTo(expected));
        _httpMessageHandler.LastRequest?.RequestUri?.AbsolutePath.Should().Be("/api/catalog/books/filters");
    }

    [Fact]
    public async Task ToResultAsync_WhenDataIsNullOnSuccess_ShouldReturnFailureWithEmptyResponseError()
    {
        // Arrange
        _httpMessageHandler.SetRawResponse(HttpStatusCode.OK, "null");

        // Act
        var actual = await _sut.GetSearchFiltersAsync(CancellationToken.None);

        // Assert
        actual.Should().BeFailure()
            .HaveError(UIErrorCodes.EmptyResponse);
    }

    [Fact]
    public async Task ToResultAsync_WhenDeserializationFails_ShouldReturnFailureWithExceptionError()
    {
        // Arrange
        _httpMessageHandler.SetRawResponse(HttpStatusCode.OK, "invalid json");

        // Act
        var actual = await _sut.GetSearchFiltersAsync(CancellationToken.None);

        // Assert
        actual.Should().BeFailure()
            .HaveError(UIErrorCodes.Exception);
    }

    [Fact]
    public async Task ToResultAsync_WhenNotSuccessfulAndNotProblemDetails_ShouldReturnFailureWithApiError()
    {
        // Arrange
        _httpMessageHandler.SetRawResponse(HttpStatusCode.InternalServerError, "Not a problem details json");

        // Act
        var actual = await _sut.GetSearchFiltersAsync(CancellationToken.None);

        // Assert
        actual.Should().BeFailure()
            .HaveError(UIErrorCodes.ApiError);
    }

    private class MockHttpMessageHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        private HttpResponseMessage _response = new(HttpStatusCode.OK);

        public void SetResponse<T>(HttpStatusCode statusCode, T content)
        {
            _response = new HttpResponseMessage(statusCode)
            {
                Content = JsonContent.Create(content)
            };
        }

        public void SetRawResponse(HttpStatusCode statusCode, string content)
        {
            _response = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(content, Encoding.UTF8, "application/json")
            };
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(_response);
        }
    }
}
