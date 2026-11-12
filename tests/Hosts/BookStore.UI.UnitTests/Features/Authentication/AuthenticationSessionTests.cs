namespace BookStore.UI.UnitTests.Features.Authentication;

public class AuthenticationSessionTests
{
    [Theory]
    [InlineData("Invalid credentials or session.", "Invalid")]
    [InlineData("Email address has not been confirmed.", "Unconfirmed")]
    public async Task LoginAsync_WhenUnauthorized_ShouldReportSafeError(string title, string expected)
    {
        // Arrange
        var handler = new Handler(_ => Response(HttpStatusCode.Unauthorized, new { title }));
        var sut = CreateSession(handler);

        // Act
        var actual = await sut.LoginAsync("customer@example.com", "password");

        // Assert
        actual.ToString().Should().Be(expected);
        (await sut.GetAuthenticationStateAsync()).User.Identity!.IsAuthenticated.Should().BeFalse();
    }

    [Fact]
    public async Task LoginAsync_WhenSuccessful_ShouldSetStateAndUseSecureRequestOptions()
    {
        // Arrange
        var handler = new Handler(_ => Token("first"));
        var sessionLock = new SessionLock();
        var sut = CreateSession(handler, sessionLock);

        // Act
        var actual = await sut.LoginAsync("customer@example.com", "password");

        // Assert
        actual.Should().Be(LoginResult.Success);
        (await sut.GetAuthenticationStateAsync()).User.Identity!.IsAuthenticated.Should().BeTrue();
        handler.Requests.Should().ContainSingle();
        handler.Requests[0].Path.Should().Be("/api/identity/login");
        handler.Requests[0].Csrf.Should().Be("1");
        handler.Requests[0].Credentials.Should().Be("include");
        handler.Requests[0].Authorization.Should().BeNull();
        sessionLock.Acquisitions.Should().Be(1);
        sessionLock.Releases.Should().Be(1);
    }

    [Fact]
    public async Task RestoreAsync_WhenSessionIsValid_ShouldRestoreOnlyOnce()
    {
        // Arrange
        var handler = new Handler(_ => Token("restored"));
        var sut = CreateSession(handler);

        // Act
        await Task.WhenAll(sut.RestoreAsync(), sut.RestoreAsync());

        // Assert
        handler.Requests.Should().ContainSingle().Which.Path.Should().Be("/api/identity/refresh");
        handler.Requests[0].Credentials.Should().Be("include");
        handler.Requests[0].Csrf.Should().Be("1");
        (await sut.GetAuthenticationStateAsync()).User.Identity!.IsAuthenticated.Should().BeTrue();
    }

    [Fact]
    public async Task RestoreAsync_WhenRefreshFails_ShouldRemainSignedOut()
    {
        // Arrange
        var handler = new Handler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var sut = CreateSession(handler);

        // Act
        await sut.RestoreAsync();

        // Assert
        (await sut.GetAuthenticationStateAsync()).User.Identity!.IsAuthenticated.Should().BeFalse();
    }

    [Fact]
    public async Task SendAuthenticatedAsync_WhenRefreshFails_ShouldClearStateWithoutRetry()
    {
        // Arrange
        var handler = new Handler(request => request.RequestUri!.AbsolutePath.EndsWith("/login")
            ? Token("first") : new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var sut = CreateSession(handler);
        await sut.LoginAsync("customer@example.com", "password");

        // Act
        using var actual = await sut.SendAuthenticatedAsync(() => new HttpRequestMessage(HttpMethod.Get, "api/orders"), TestContext.Current.CancellationToken);

        // Assert
        actual.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        handler.Requests.Count(request => request.Path == "/api/orders").Should().Be(1);
        handler.Requests.Count(request => request.Path == "/api/identity/refresh").Should().Be(1);
        (await sut.GetAuthenticationStateAsync()).User.Identity!.IsAuthenticated.Should().BeFalse();
    }

    [Fact]
    public async Task LoginAsync_WhenResponseIsMalformed_ShouldRemainSignedOut()
    {
        // Arrange
        var handler = new Handler(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{") });
        var sut = CreateSession(handler);

        // Act
        var actual = await sut.LoginAsync("customer@example.com", "password");

        // Assert
        actual.Should().Be(LoginResult.Unavailable);
        (await sut.GetAuthenticationStateAsync()).User.Identity!.IsAuthenticated.Should().BeFalse();
    }

    [Fact]
    public async Task SendAuthenticatedAsync_WhenConcurrentRequestsAreUnauthorized_ShouldRefreshOnceAndRetry()
    {
        // Arrange
        var firstRequests = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var count = 0;
        var refreshCount = 0;
        var handler = new Handler(async request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/login"))
            {
                return Token("first");
            }
            if (request.RequestUri.AbsolutePath.EndsWith("/refresh"))
            {
                Interlocked.Increment(ref refreshCount);
                return Token("second");
            }
            if (request.Headers.Authorization?.Parameter == "first")
            {
                if (Interlocked.Increment(ref count) == 2)
                {
                    firstRequests.SetResult();
                }
                await firstRequests.Task;
                return new HttpResponseMessage(HttpStatusCode.Unauthorized);
            }

            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var sut = CreateSession(handler);
        await sut.LoginAsync("customer@example.com", "password");

        // Act
        var actual = await Task.WhenAll(
            sut.SendAuthenticatedAsync(() => new HttpRequestMessage(HttpMethod.Get, "api/orders"), TestContext.Current.CancellationToken),
            sut.SendAuthenticatedAsync(() => new HttpRequestMessage(HttpMethod.Get, "api/orders"), TestContext.Current.CancellationToken));

        // Assert
        actual.Should().OnlyContain(response => response.StatusCode == HttpStatusCode.OK);
        refreshCount.Should().Be(1);
        handler.Requests.Count(request => request.Path == "/api/orders").Should().Be(4);
        handler.Requests.Count(request => request.Authorization == "Bearer second").Should().Be(2);
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task SendAuthenticatedAsync_WhenUnsafeRequestIsUnauthorized_ShouldNotRetry(string method)
    {
        // Arrange
        var handler = new Handler(request => request.RequestUri!.AbsolutePath switch
        {
            "/api/identity/login" => Token("first"),
            "/api/identity/refresh" => Token("second"),
            _ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        });
        var sut = CreateSession(handler);
        await sut.LoginAsync("customer@example.com", "password");

        // Act
        using var actual = await sut.SendAuthenticatedAsync(() => new HttpRequestMessage(new HttpMethod(method), "api/orders"), TestContext.Current.CancellationToken);

        // Assert
        actual.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        handler.Requests.Count(request => request.Path == "/api/orders").Should().Be(1);
        handler.Requests.Count(request => request.Path == "/api/identity/refresh").Should().Be(1);
    }

    [Fact]
    public async Task SendAuthenticatedAsync_WhenRetryIsUnauthorized_ShouldNotLoop()
    {
        // Arrange
        var handler = new Handler(request => request.RequestUri!.AbsolutePath switch
        {
            "/api/identity/login" => Token("first"),
            "/api/identity/refresh" => Token("second"),
            _ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        });
        var sut = CreateSession(handler);
        await sut.LoginAsync("customer@example.com", "password");

        // Act
        using var actual = await sut.SendAuthenticatedAsync(() => new HttpRequestMessage(HttpMethod.Get, "api/orders"), TestContext.Current.CancellationToken);

        // Assert
        actual.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        handler.Requests.Count(request => request.Path == "/api/orders").Should().Be(2);
        handler.Requests.Count(request => request.Path == "/api/identity/refresh").Should().Be(1);
    }

    [Theory]
    [InlineData("https://example.com/api/orders")]
    [InlineData("//example.com/api/orders")]
    [InlineData("other/path")]
    public async Task SendAuthenticatedAsync_WhenUriIsNotRelativeApiPath_ShouldRejectRequest(string uri)
    {
        // Arrange
        var handler = new Handler(_ => Token("first"));
        var sut = CreateSession(handler);
        await sut.LoginAsync("customer@example.com", "password");

        // Act
        var act = () => sut.SendAuthenticatedAsync(() => new HttpRequestMessage(HttpMethod.Get, uri), TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        handler.Requests.Should().ContainSingle();
    }

    [Theory]
    [InlineData(HttpStatusCode.NoContent, true)]
    [InlineData(HttpStatusCode.Forbidden, false)]
    public async Task LogoutAsync_WhenServerResponds_ShouldClearState(HttpStatusCode status, bool expected)
    {
        // Arrange
        var handler = new Handler(request => request.RequestUri!.AbsolutePath.EndsWith("/login")
            ? Token("first") : new HttpResponseMessage(status));
        var sut = CreateSession(handler);
        await sut.LoginAsync("customer@example.com", "password");

        // Act
        var actual = await sut.LogoutAsync();

        // Assert
        actual.Should().Be(expected);
        (await sut.GetAuthenticationStateAsync()).User.Identity!.IsAuthenticated.Should().BeFalse();
        handler.Requests.Last().Path.Should().Be("/api/identity/logout");
        handler.Requests.Last().Credentials.Should().Be("include");
        handler.Requests.Last().Csrf.Should().Be("1");
    }

    [Fact]
    public async Task LogoutAsync_WhenNetworkFails_ShouldClearStateAndReportFailure()
    {
        // Arrange
        var handler = new Handler(request => request.RequestUri!.AbsolutePath.EndsWith("/login")
            ? Token("first") : throw new HttpRequestException());
        var sut = CreateSession(handler);
        await sut.LoginAsync("customer@example.com", "password");

        // Act
        var actual = await sut.LogoutAsync();

        // Assert
        actual.Should().BeFalse();
        (await sut.GetAuthenticationStateAsync()).User.Identity!.IsAuthenticated.Should().BeFalse();
    }

    [Fact]
    public async Task LoginAsync_WhenLockIsUnavailable_ShouldFailClosed()
    {
        // Arrange
        var handler = new Handler(_ => Token("first"));
        var sessionLock = new SessionLock { Unavailable = true };
        var sut = CreateSession(handler, sessionLock);

        // Act
        var actual = await sut.LoginAsync("customer@example.com", "password");

        // Assert
        actual.Should().Be(LoginResult.Unavailable);
        handler.Requests.Should().BeEmpty();
        (await sut.GetAuthenticationStateAsync()).User.Identity!.IsAuthenticated.Should().BeFalse();
    }

    private static AuthenticationSession CreateSession(Handler handler, ISessionLock? sessionLock = null) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com/") },
            sessionLock ?? new SessionLock());

    private static HttpResponseMessage Token(string token) => Response(HttpStatusCode.OK, new { accessToken = token });

    private static HttpResponseMessage Response(HttpStatusCode status, object content) =>
        new(status) { Content = JsonContent.Create(content) };

    private sealed record RequestSnapshot(string Path, string? Authorization, string? Csrf, string? Credentials);

    private sealed class SessionLock : ISessionLock
    {
        internal int Acquisitions { get; private set; }
        internal int Releases { get; private set; }
        internal bool Unavailable { get; init; }

        public Task AcquireAsync()
        {
            if (Unavailable)
            {
                throw new JSException("Lock unavailable");
            }

            Acquisitions++;
            return Task.CompletedTask;
        }

        public Task ReleaseAsync()
        {
            Releases++;
            return Task.CompletedTask;
        }
    }

    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        private readonly object _sync = new();
        internal List<RequestSnapshot> Requests { get; } = [];

        internal Handler(Func<HttpRequestMessage, HttpResponseMessage> send) : this(request => Task.FromResult(send(request))) { }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Options.TryGetValue(new HttpRequestOptionsKey<IDictionary<string, object>>("WebAssemblyFetchOptions"), out var options);
            var credentials = options is not null && options.TryGetValue("credentials", out var value) ? value.ToString() : null;
            var csrf = request.Headers.TryGetValues("X-BookStore-Request", out var values) ? values.Single() : null;
            lock (_sync)
            {
                Requests.Add(new(request.RequestUri!.AbsolutePath, request.Headers.Authorization?.ToString(), csrf, credentials));
            }

            return send(request);
        }
    }
}
