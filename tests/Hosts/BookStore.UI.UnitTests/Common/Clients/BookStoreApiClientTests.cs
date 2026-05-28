namespace BookStore.UI.UnitTests.Common.Clients;

public class BookStoreApiClientTests
{
    private readonly IHttpClientFactory _httpClientFactory = Substitute.For<IHttpClientFactory>();
    private readonly MockHttpMessageHandler _httpMessageHandler = new();
    private readonly HttpClient _httpClient;
    private readonly BookStoreApiClient _sut;

    public BookStoreApiClientTests()
    {
        _httpClient = new HttpClient(_httpMessageHandler) { BaseAddress = new Uri("http://localhost/") };
        _httpClientFactory.CreateClient("bookstore-api").Returns(_httpClient);
        _sut = new BookStoreApiClient(_httpClientFactory);
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

    private class MockHttpMessageHandler : HttpMessageHandler
    {
        private HttpResponseMessage _response = new(HttpStatusCode.OK);

        public void SetResponse<T>(HttpStatusCode statusCode, T content)
        {
            _response = new HttpResponseMessage(statusCode)
            {
                Content = JsonContent.Create(content)
            };
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) 
            => Task.FromResult(_response);
    }
}
