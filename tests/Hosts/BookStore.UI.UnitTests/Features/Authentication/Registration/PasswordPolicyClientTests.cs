namespace BookStore.UI.UnitTests.Features.Authentication.Registration;

public class PasswordPolicyClientTests
{
    [Theory]
    [InlineData("{}")]
    [InlineData("{\"schemaVersion\":2,\"requiredLength\":6,\"requiredUniqueChars\":1,\"requireDigit\":true,\"requireLowercase\":true,\"requireUppercase\":true,\"requireNonAlphanumeric\":true}")]
    [InlineData("invalid json")]
    public async Task GetAsync_WhenPolicyIsInvalid_ShouldReturnNullAndLogWarning(string json)
    {
        // Arrange
        var logger = new RecordingLogger();
        var client = new HttpClient(new Handler(() => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") }))
        {
            BaseAddress = new Uri("https://api.example.com")
        };
        var sut = new PasswordPolicyClient(client, logger);

        // Act
        var actual = await sut.GetAsync();

        // Assert
        actual.Should().BeNull();
        logger.Messages.Should().ContainSingle().Which.Should().Contain("Password policy could not");
    }

    [Fact]
    public async Task GetAsync_WhenNetworkFails_ShouldReturnNull()
    {
        // Arrange
        var client = new HttpClient(new Handler(() => throw new HttpRequestException())) { BaseAddress = new Uri("https://api.example.com") };
        var sut = new PasswordPolicyClient(client, new RecordingLogger());

        // Act
        var actual = await sut.GetAsync();

        // Assert
        actual.Should().BeNull();
    }

    private sealed class Handler(Func<HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond());
    }

    private sealed class RecordingLogger : ILogger<PasswordPolicyClient>
    {
        internal List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => logLevel == LogLevel.Warning;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }
}
