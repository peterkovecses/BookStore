namespace BookStore.Identity.IntegrationTests;

[Collection("Identity Integration")]
public class SmtpOutboxPreflightTests
{
    [Fact]
    public async Task CheckAsync_WhenCircuitIsOpen_ShouldExposeRecoveryTime()
    {
        // Arrange
        var retryAfter = DateTimeOffset.UtcNow.AddMinutes(2);
        var availability = Substitute.For<IEmailDispatchAvailability>();
        availability.GetAvailability().Returns(new EmailDispatchAvailability(false, retryAfter));
        var sut = new SmtpOutboxPreflight<OutboxWorkerTests.TestEvent>(availability);

        // Act
        var actual = await sut.CheckAsync(new OutboxWorkerTests.TestEvent(), TestContext.Current.CancellationToken);

        // Assert
        actual.Available.Should().BeFalse();
        actual.RetryAfter.Should().Be(retryAfter);
    }
}
