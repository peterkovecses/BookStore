namespace BookStore.Common.UnitTests.Outbox;

public class OutboxCleanerTests
{
    [Fact]
    public async Task ExecuteAsync_WhenCleanupFails_ShouldRetryOnNextInterval()
    {
        // Arrange
        var store = Substitute.For<IOutboxStore>();
        var recovered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        store.CleanupAsync(Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                if (Interlocked.Increment(ref calls) == 1)
                {
                    return Task.FromException<int>(new InvalidOperationException());
                }

                recovered.TrySetResult();

                return Task.FromResult(0);
            });
        var services = new ServiceCollection().AddScoped(_ => store).BuildServiceProvider();
        var logger = Substitute.For<ILogger<OutboxCleaner<IOutboxStore>>>();
        logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        var options = Options.Create(new OutboxOptions { CleanupInterval = TimeSpan.FromMilliseconds(20) });
        var sut = new OutboxCleaner<IOutboxStore>(services.GetRequiredService<IServiceScopeFactory>(),
            options, TimeProvider.System, logger);

        // Act
        await sut.StartAsync(TestContext.Current.CancellationToken);
        await recovered.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await sut.StopAsync(TestContext.Current.CancellationToken);
        sut.Dispose();
        services.Dispose();

        // Assert
        calls.Should().BeGreaterThanOrEqualTo(2);
        logger.ReceivedCalls().Should().Contain(call =>
            call.GetMethodInfo().Name == nameof(ILogger.Log) && Equals(call.GetArguments()[0], LogLevel.Error));
    }
}
