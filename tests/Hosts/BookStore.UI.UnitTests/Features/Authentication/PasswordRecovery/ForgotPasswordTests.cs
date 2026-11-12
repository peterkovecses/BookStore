namespace BookStore.UI.UnitTests.Features.Authentication.PasswordRecovery;

public sealed class ForgotPasswordTests : BunitTestContext
{
    [Fact]
    public void Submit_WhenAccepted_ShouldCountDownAndRestartAfterExpiry()
    {
        // Arrange
        var clock = new FakeTimeProvider();
        var handler = new Handler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        var sut = RenderPage(clock, handler);
        sut.Find("#email").Change("jane@example.com");

        // Act
        sut.Find("form").Submit();

        // Assert
        sut.Markup.Should().Contain("60 seconds").And.Contain("Delivery is not guaranteed");
        sut.Find("button[type=submit]").HasAttribute("disabled").Should().BeTrue();
        clock.Advance(TimeSpan.FromSeconds(59));
        sut.WaitForAssertion(() => sut.Markup.Should().Contain("1 second."));
        clock.Advance(TimeSpan.FromSeconds(1));
        sut.WaitForAssertion(() => sut.Find("button[type=submit]").HasAttribute("disabled").Should().BeFalse());
        sut.Find("form").Submit();
        sut.Markup.Should().Contain("60 seconds");
        handler.Count.Should().Be(2);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "valid email")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "could not be completed")]
    [InlineData(HttpStatusCode.TooManyRequests, "Too many requests")]
    public void Submit_WhenRequestFails_ShouldShowErrorWithoutCooldown(HttpStatusCode status, string expected)
    {
        // Arrange
        var sut = RenderPage(new FakeTimeProvider(), new Handler(_ => new HttpResponseMessage(status)));
        sut.Find("#email").Change("jane@example.com");

        // Act
        sut.Find("form").Submit();

        // Assert
        sut.Markup.Should().Contain(expected);
        sut.Find("button[type=submit]").HasAttribute("disabled").Should().BeFalse();
    }

    [Fact]
    public void Submit_WhenServerReturnsRetryAfter_ShouldShowRemainingWait()
    {
        // Arrange
        var clock = new FakeTimeProvider();
        var handler = new Handler(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(7));

            return response;
        });
        var sut = RenderPage(clock, handler);
        sut.Find("#email").Change("jane@example.com");

        // Act
        sut.Find("form").Submit();

        // Assert
        sut.Markup.Should().Contain("Too many requests").And.Contain("7 seconds");
        clock.Advance(TimeSpan.FromSeconds(7));
        sut.WaitForAssertion(() => sut.Find("button[type=submit]").HasAttribute("disabled").Should().BeFalse());
    }

    [Fact]
    public void Open_WhenStoredCooldownExists_ShouldRestoreAndDisposeTimer()
    {
        // Arrange
        var clock = new FakeTimeProvider();
        var expiry = clock.GetUtcNow().AddSeconds(10).ToUnixTimeMilliseconds().ToString();
        var handler = new Handler(_ => new HttpResponseMessage(HttpStatusCode.NoContent));
        var sut = RenderPage(clock, handler, expiry);

        // Act
        sut.Find("#email").Change("jane@example.com");
        sut.Find("form").Submit();

        // Assert
        sut.Markup.Should().Contain("10 seconds");
        handler.Count.Should().Be(0);
        sut.Dispose();
        clock.Advance(TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void Submit_WhenRequestIsInFlight_ShouldNotSendDuplicate()
    {
        // Arrange
        var pending = new TaskCompletionSource<HttpResponseMessage>();
        var handler = new Handler(_ => pending.Task);
        var sut = RenderPage(new FakeTimeProvider(), handler);
        sut.Find("#email").Change("jane@example.com");

        // Act
        sut.Find("form").Submit();
        sut.Find("form").Submit();

        // Assert
        handler.Count.Should().Be(1);
        sut.Find("button[type=submit]").HasAttribute("disabled").Should().BeTrue();
        pending.SetResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        sut.WaitForAssertion(() => sut.Find("button[type=submit]").HasAttribute("disabled").Should().BeFalse());
    }

    private IRenderedComponent<ForgotPassword> RenderPage(FakeTimeProvider clock, Handler handler, string? stored = null)
    {
        JSInterop.Setup<string?>("sessionStorage.getItem", _ => true).SetResult(stored);
        JSInterop.SetupVoid("sessionStorage.setItem", _ => true).SetVoidResult();
        Services.AddSingleton<TimeProvider>(clock);
        Services.AddSingleton(new PasswordRecoveryClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.example.com") }));

        return RenderComponent<ForgotPassword>();
    }

    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        internal Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : this(request => Task.FromResult(respond(request))) { }
        internal int Count { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Count++;

            return respond(request);
        }
    }
}
