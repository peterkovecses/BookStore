namespace BookStore.UI.UnitTests.Features.Authentication.Shared;

public class RegistrationClientTests
{
    [Fact]
    public async Task RegisterAsync_WhenAccepted_ShouldSendFieldsAndReturnNeutralSuccess()
    {
        // Arrange
        var handler = new Handler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        var sut = CreateClient(handler);
        var model = new RegistrationModel { Email = "jane@example.com", FirstName = "Jane", LastName = "Doe", Password = "Password1!", ConfirmPassword = "confirmation-not-sent" };

        // Act
        var actual = await sut.RegisterAsync(model);

        // Assert
        actual.Status.Should().Be(RegistrationStatus.Success);
        handler.Path.Should().Be("/api/identity/registration");
        handler.Body.Should().Contain("\"email\":\"jane@example.com\"").And.Contain("\"firstName\":\"Jane\"")
            .And.Contain("\"lastName\":\"Doe\"").And.Contain("\"password\":\"Password1!\"");
        handler.Body.Should().NotContain("confirmPassword").And.NotContain("confirmation-not-sent");
    }

    [Fact]
    public async Task RegisterAsync_WhenBackendRejectsFields_ShouldReturnValidationErrors()
    {
        // Arrange
        var handler = new Handler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = JsonContent.Create(new { errors = new Dictionary<string, string[]> { ["Password"] = ["Password must contain a digit."] } })
        });
        var sut = CreateClient(handler);

        // Act
        var actual = await sut.RegisterAsync(new RegistrationModel());

        // Assert
        actual.Status.Should().Be(RegistrationStatus.Invalid);
        actual.Errors.Should().ContainKey("Password").WhoseValue.Should().ContainSingle().Which.Should().Be("Password must contain a digit.");
    }

    [Theory]
    [InlineData(HttpStatusCode.NoContent, "Success")]
    [InlineData(HttpStatusCode.BadRequest, "Invalid")]
    [InlineData(HttpStatusCode.TooManyRequests, "Unavailable")]
    [InlineData(HttpStatusCode.InternalServerError, "Unavailable")]
    public async Task ConfirmAsync_WhenServerResponds_ShouldReturnSafeStatus(HttpStatusCode response, string expected)
    {
        // Arrange
        var handler = new Handler(_ => new HttpResponseMessage(response));
        var sut = CreateClient(handler);
        var link = new IdentityLink("user-1", "abc_DEF-123");

        // Act
        var actual = await sut.ConfirmAsync(link);

        // Assert
        actual.ToString().Should().Be(expected);
        handler.Path.Should().Be("/api/identity/verification/confirmation");
        handler.Body.Should().Contain("\"userId\":\"user-1\"").And.Contain("\"token\":\"abc_DEF-123\"");
    }

    [Theory]
    [InlineData("", "abc")]
    [InlineData("user", "")]
    [InlineData("user", "bad+token")]
    [InlineData("user", "bad%token")]
    public async Task ConfirmAsync_WhenLinkIsMalformed_ShouldNotSendRequest(string userId, string token)
    {
        // Arrange
        var handler = new Handler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        var sut = CreateClient(handler);

        // Act
        var actual = await sut.ConfirmAsync(new IdentityLink(userId, token));

        // Assert
        actual.Should().Be(VerificationStatus.Invalid);
        handler.Path.Should().BeNull();
    }

    [Theory]
    [InlineData(HttpStatusCode.NoContent, "Success")]
    [InlineData(HttpStatusCode.BadRequest, "Invalid")]
    [InlineData(HttpStatusCode.TooManyRequests, "RateLimited")]
    public async Task ResendAsync_WhenServerResponds_ShouldReturnSafeStatus(HttpStatusCode response, string expected)
    {
        // Arrange
        const string email = "jane@example.com";
        var handler = new Handler(_ => new HttpResponseMessage(response));
        var sut = CreateClient(handler);

        // Act
        var actual = await sut.ResendAsync(email);

        // Assert
        actual.Status.ToString().Should().Be(expected);
        handler.Path.Should().Be("/api/identity/verification/resend");
        handler.Body.Should().Contain(email);
    }

    [Fact]
    public async Task ResendAsync_WhenRateLimitedWithRetryAfter_ShouldReturnRemainingSeconds()
    {
        // Arrange
        const int expected = 17;
        var handler = new Handler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(expected));

            return response;
        });
        var sut = CreateClient(handler);

        // Act
        var actual = await sut.ResendAsync("jane@example.com");

        // Assert
        actual.Status.Should().Be(VerificationStatus.RateLimited);
        actual.RetryAfterSeconds.Should().Be(expected);
    }

    [Fact]
    public async Task ConfirmAsync_WhenNetworkFails_ShouldReportUnavailable()
    {
        // Arrange
        var handler = new Handler(_ => throw new HttpRequestException());
        var sut = CreateClient(handler);

        // Act
        var actual = await sut.ConfirmAsync(new IdentityLink("user", "token"));

        // Assert
        actual.Should().Be(VerificationStatus.Unavailable);
    }

    private static RegistrationClient CreateClient(Handler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com/") });

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        internal string? Path { get; private set; }
        internal string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Path = request.RequestUri!.AbsolutePath;
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);

            return respond(request);
        }
    }
}
