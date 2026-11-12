namespace BookStore.UI.UnitTests.Features.Authentication.Shared;

public class AuthenticationPagesTests
{
    [Fact]
    public async Task Register_WhenOpened_ShouldRenderRequiredFields()
    {
        // Arrange
        using var services = CreateServices();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());

        // Act
        var actual = await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<Register>()).ToHtmlString());

        // Assert
        actual.Should().Contain("id=\"firstName\"").And.Contain("id=\"lastName\"")
            .And.Contain("id=\"email\"").And.Contain("id=\"password\"").And.Contain("id=\"confirmPassword\"");
    }

    [Fact]
    public async Task VerificationPending_WhenOpened_ShouldExplainNeutralResend()
    {
        // Arrange
        using var services = CreateServices();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());

        // Act
        var actual = await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<VerificationPending>()).ToHtmlString());

        // Assert
        actual.Should().Contain("If the address is eligible").And.Contain("Delivery is not guaranteed")
            .And.Contain("id=\"resendEmail\"");
    }

    [Theory]
    [InlineData(HttpStatusCode.NoContent, "Your email is confirmed", "href=\"login\"")]
    [InlineData(HttpStatusCode.BadRequest, "invalid or expired", "href=\"verification-pending\"")]
    [InlineData(HttpStatusCode.TooManyRequests, "confirmation is unavailable", "href=\"verification-pending\"")]
    public async Task VerifyEmail_WhenApiResponds_ShouldRenderSafeFeedback(HttpStatusCode response, string expected, string expectedLink)
    {
        // Arrange
        var js = Substitute.For<IJSRuntime>();
        js.InvokeAsync<IdentityLink?>("bookStoreVerificationLink.take", Arg.Any<object?[]?>())
            .Returns(new ValueTask<IdentityLink?>(new IdentityLink("user", "encoded-token")));
        using var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton(js)
            .AddSingleton(new RegistrationClient(new HttpClient(new Handler(response)) { BaseAddress = new Uri("https://api.example.com") }))
            .BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());

        // Act
        var actual = await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<VerifyEmail>()).ToHtmlString());

        // Assert
        actual.Should().Contain(expected).And.Contain(expectedLink).And.NotContain("encoded-token");
    }

    [Fact]
    public async Task VerifyEmail_WhenLinkIsMissing_ShouldOfferResend()
    {
        // Arrange
        var js = Substitute.For<IJSRuntime>();
        js.InvokeAsync<IdentityLink?>("bookStoreVerificationLink.take", Arg.Any<object?[]?>())
            .Returns(new ValueTask<IdentityLink?>((IdentityLink?)null));
        using var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton(js)
            .AddSingleton(new RegistrationClient(new HttpClient(new Handler(HttpStatusCode.NoContent)) { BaseAddress = new Uri("https://api.example.com") }))
            .BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());

        // Act
        var actual = await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<VerifyEmail>()).ToHtmlString());

        // Assert
        actual.Should().Contain("invalid or expired").And.Contain("href=\"verification-pending\"");
    }

    [Fact]
    public async Task ForgotPassword_WhenOpened_ShouldExplainNeutralResponse()
    {
        // Arrange
        using var services = CreatePasswordRecoveryServices();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());

        // Act
        var actual = await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<ForgotPassword>()).ToHtmlString());

        // Assert
        actual.Should().Contain("If the address is eligible").And.Contain("id=\"email\"")
            .And.Contain("href=\"login\"");
    }

    [Fact]
    public async Task ResetPassword_WhenLinkIsMissing_ShouldOfferNewLinkWithoutPasswordForm()
    {
        // Arrange
        var js = Substitute.For<IJSRuntime>();
        js.InvokeAsync<IdentityLink?>("bookStoreVerificationLink.takePasswordReset", Arg.Any<object?[]?>())
            .Returns(new ValueTask<IdentityLink?>((IdentityLink?)null));
        using var services = CreatePasswordRecoveryServices(js);
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());

        // Act
        var actual = await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<ResetPassword>()).ToHtmlString());

        // Assert
        actual.Should().Contain("invalid, expired or already used").And.Contain("href=\"forgot-password\"")
            .And.NotContain("id=\"newPassword\"");
    }

    [Fact]
    public async Task ResetPassword_WhenLinkIsValid_ShouldRenderPasswordFieldsWithoutLinkData()
    {
        // Arrange
        var js = Substitute.For<IJSRuntime>();
        js.InvokeAsync<IdentityLink?>("bookStoreVerificationLink.takePasswordReset", Arg.Any<object?[]?>())
            .Returns(new ValueTask<IdentityLink?>(new IdentityLink("user-1", "encoded-token")));
        using var services = CreatePasswordRecoveryServices(js);
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());

        // Act
        var actual = await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<ResetPassword>()).ToHtmlString());

        // Assert
        actual.Should().Contain("id=\"newPassword\"").And.Contain("id=\"confirmPassword\"")
            .And.NotContain("encoded-token").And.NotContain("user-1");
    }

    [Theory]
    [InlineData("", "encoded-token")]
    [InlineData("user-1", "")]
    [InlineData("user-1", "invalid%token")]
    [InlineData("user-1", "invalid token")]
    public async Task ResetPassword_WhenLinkIsMalformed_ShouldOfferNewLinkWithoutPasswordForm(string userId, string token)
    {
        // Arrange
        var js = Substitute.For<IJSRuntime>();
        js.InvokeAsync<IdentityLink?>("bookStoreVerificationLink.takePasswordReset", Arg.Any<object?[]?>())
            .Returns(new ValueTask<IdentityLink?>(new IdentityLink(userId, token)));
        using var services = CreatePasswordRecoveryServices(js);
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());

        // Act
        var actual = await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync<ResetPassword>()).ToHtmlString());

        // Assert
        actual.Should().Contain("invalid, expired or already used").And.Contain("href=\"forgot-password\"")
            .And.NotContain("id=\"newPassword\"");
    }

    private sealed class Handler(HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status));
    }

    private static ServiceProvider CreateServices() =>
        new ServiceCollection()
            .AddLogging()
            .AddSingleton<NavigationManager>(new TestNavigationManager())
            .AddSingleton(new RegistrationClient(new HttpClient(new Handler(HttpStatusCode.NoContent)) { BaseAddress = new Uri("https://api.example.com") }))
            .AddSingleton(provider => new PasswordPolicyClient(
                new HttpClient(new Handler(HttpStatusCode.NoContent)) { BaseAddress = new Uri("https://api.example.com") },
                provider.GetRequiredService<ILogger<PasswordPolicyClient>>()))
            .BuildServiceProvider();

    private static ServiceProvider CreatePasswordRecoveryServices(IJSRuntime? js = null) =>
        new ServiceCollection()
            .AddLogging()
            .AddSingleton(js ?? Substitute.For<IJSRuntime>())
            .AddSingleton(new PasswordRecoveryClient(new HttpClient(new Handler(HttpStatusCode.NoContent)) { BaseAddress = new Uri("https://api.example.com") }))
            .AddSingleton(new AuthenticationSession(new HttpClient(new Handler(HttpStatusCode.NoContent)) { BaseAddress = new Uri("https://api.example.com") }, new TestSessionLock()))
            .BuildServiceProvider();

    private sealed class TestSessionLock : ISessionLock
    {
        public Task AcquireAsync() => Task.CompletedTask;
        public Task ReleaseAsync() => Task.CompletedTask;
    }

    private sealed class TestNavigationManager : NavigationManager
    {
        internal TestNavigationManager() => Initialize("https://example.com/", "https://example.com/");
    }
}
