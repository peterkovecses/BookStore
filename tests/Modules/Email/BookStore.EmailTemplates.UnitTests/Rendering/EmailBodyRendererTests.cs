namespace BookStore.EmailTemplates.UnitTests.Rendering;

public sealed class EmailBodyRendererTests
{
    [Theory]
    [InlineData(true, "Verify your email address", "Verify email address")]
    [InlineData(false, "Reset your password", "Reset password")]
    public async Task RenderAsync_WhenInputIsHostile_ShouldEncodeHtmlAndPreserveTextAndLink(
        bool verification, string heading, string action)
    {
        // Arrange
        const string firstName = "<Alice & \"Bob\"> <script>alert('x')</script>";
        const string link = "https://example.com/action?token=a%2Bb&next=\"'><img src=x onerror=alert(1)>";
        using var services = new ServiceCollection().BuildServiceProvider();
        var sut = new EmailBodyRenderer(services, NullLoggerFactory.Instance);

        // Act
        var actual = verification
            ? await sut.RenderVerificationAsync(firstName, link, TestContext.Current.CancellationToken)
            : await sut.RenderPasswordResetAsync(firstName, link, TestContext.Current.CancellationToken);

        // Assert
        actual.TextBody.Should().Contain("BookStore").And.Contain(heading).And.Contain($"Hello {firstName},")
            .And.Contain(action).And.Contain(link).And.Contain("If you did not request this");
        actual.HtmlBody.Should().Contain("BookStore").And.Contain(heading).And.Contain(action)
            .And.Contain("If you did not request this").And.NotContain(firstName).And.NotContain("<script>")
            .And.NotContain("<img");
        WebUtility.HtmlDecode(actual.HtmlBody).Should().Contain($"Hello {firstName},").And.Contain(link);
        var href = Regex.Match(actual.HtmlBody, "href=\"([^\"]*)\"").Groups[1].Value;
        WebUtility.HtmlDecode(href).Should().Be(link);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RenderAsync_WhenCancellationWasRequested_ShouldThrowOperationCanceledException(bool verification)
    {
        // Arrange
        using var services = new ServiceCollection().BuildServiceProvider();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var sut = new EmailBodyRenderer(services, NullLoggerFactory.Instance);
        const string firstName = "Alice";
        const string link = "https://example.com/action";

        // Act
        Func<Task> act = () => verification
            ? sut.RenderVerificationAsync(firstName, link, cancellation.Token)
            : sut.RenderPasswordResetAsync(firstName, link, cancellation.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
