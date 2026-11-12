namespace BookStore.UI.UnitTests.Features.Authentication.PasswordRecovery;

public class PasswordRecoveryClientTests
{
    [Fact]
    public async Task RequestAsync_WhenAccepted_ShouldSendEmailToForgotEndpoint()
    {
        // Arrange
        const string email = "jane@example.com";
        var handler = new Handler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        var sut = CreateClient(handler);

        // Act
        var actual = await sut.RequestAsync(email);

        // Assert
        actual.Should().Be(PasswordRecoveryRequestStatus.Success);
        handler.Path.Should().Be("/api/identity/password/forgot");
        handler.Body.Should().Contain(email);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "Invalid")]
    [InlineData(HttpStatusCode.TooManyRequests, "Unavailable")]
    [InlineData(HttpStatusCode.InternalServerError, "Unavailable")]
    public async Task RequestAsync_WhenServerRejectsRequest_ShouldReturnSafeStatus(HttpStatusCode response, string expected)
    {
        // Arrange
        var handler = new Handler(_ => new HttpResponseMessage(response));
        var sut = CreateClient(handler);

        // Act
        var actual = await sut.RequestAsync("jane@example.com");

        // Assert
        actual.ToString().Should().Be(expected);
    }

    [Fact]
    public async Task RequestAsync_WhenNetworkFails_ShouldReportUnavailable()
    {
        // Arrange
        var handler = new Handler(_ => throw new HttpRequestException());
        var sut = CreateClient(handler);

        // Act
        var actual = await sut.RequestAsync("jane@example.com");

        // Assert
        actual.Should().Be(PasswordRecoveryRequestStatus.Unavailable);
    }

    [Fact]
    public async Task ResetAsync_WhenAccepted_ShouldSendLinkAndPasswordToResetEndpoint()
    {
        // Arrange
        const string userId = "user-1";
        const string token = "abc_DEF-123";
        const string password = "Password1!";
        var handler = new Handler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        var sut = CreateClient(handler);

        // Act
        var actual = await sut.ResetAsync(new IdentityLink(userId, token), password);

        // Assert
        actual.Should().Be(PasswordResetStatus.Success);
        handler.Path.Should().Be("/api/identity/password/reset");
        handler.Body.Should().Contain("\"userId\":\"user-1\"").And.Contain("\"token\":\"abc_DEF-123\"")
            .And.Contain("\"newPassword\":\"Password1!\"");
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "Invalid")]
    [InlineData(HttpStatusCode.TooManyRequests, "Unavailable")]
    [InlineData(HttpStatusCode.InternalServerError, "Unavailable")]
    public async Task ResetAsync_WhenServerRejectsRequest_ShouldReturnSafeStatus(HttpStatusCode response, string expected)
    {
        // Arrange
        var handler = new Handler(_ => new HttpResponseMessage(response));
        var sut = CreateClient(handler);

        // Act
        var actual = await sut.ResetAsync(new IdentityLink("user-1", "abc_DEF-123"), "Password1!");

        // Assert
        actual.ToString().Should().Be(expected);
    }

    [Theory]
    [InlineData(null, "Password1!")]
    [InlineData("bad+token", "Password1!")]
    [InlineData("abc_DEF-123", "")]
    public async Task ResetAsync_WhenLinkOrPasswordIsMalformed_ShouldNotSendRequest(string? token, string password)
    {
        // Arrange
        var handler = new Handler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        var sut = CreateClient(handler);
        var link = token is null ? null : new IdentityLink("user-1", token);

        // Act
        var actual = await sut.ResetAsync(link, password);

        // Assert
        actual.Should().Be(PasswordResetStatus.Invalid);
        handler.Path.Should().BeNull();
    }

    [Fact]
    public async Task ResetAsync_WhenNetworkFails_ShouldReportUnavailable()
    {
        // Arrange
        var handler = new Handler(_ => throw new HttpRequestException());
        var sut = CreateClient(handler);

        // Act
        var actual = await sut.ResetAsync(new IdentityLink("user-1", "abc_DEF-123"), "Password1!");

        // Assert
        actual.Should().Be(PasswordResetStatus.Unavailable);
    }

    private static PasswordRecoveryClient CreateClient(Handler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com/") });

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        internal string? Path { get; private set; }
        internal string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Path = request.RequestUri!.AbsolutePath;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);

            return respond(request);
        }
    }
}
