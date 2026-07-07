namespace BookStore.Common.UnitTests.Caching;

file sealed record TestRequest : IRequest<Result<string>>;

public class QueryCachingBehaviorTests
{
    private readonly HybridCache _cache;
    private readonly QueryCachingBehavior<TestCachedRequest, Result<string>> _cachingSut;

    public QueryCachingBehaviorTests()
    {
        _cache = Substitute.For<HybridCache>();
        _cachingSut = new QueryCachingBehavior<TestCachedRequest, Result<string>>(_cache);
    }

    [Fact]
    public async Task HandleAsync_WhenRequestIsNotCachedQuery_ShouldCallNext()
    {
        // Arrange
        var sut = new QueryCachingBehavior<TestRequest, Result<string>>(_cache);
        var request = new TestRequest();
        var next = Substitute.For<RequestHandlerDelegate<Result<string>>>();
        var expected = Result.Success("data");
        next().Returns(expected);

        // Act
        var actual = await sut.HandleAsync(request, next, TestContext.Current.CancellationToken);

        // Assert
        actual.Should().WhichData.Should().Be("data");
        await next.Received(1)();
        await VerifyCacheWasNotCalled();
    }

    [Fact]
    public async Task HandleAsync_WhenCacheMiss_ShouldCallNextAndCacheResult()
    {
        // Arrange
        var request = new TestCachedRequest("key_1");
        var next = Substitute.For<RequestHandlerDelegate<Result<string>>>();
        var expected = Result.Success("cached_value");
        next().Returns(expected);

        SetupCacheMiss("key_1");

        // Act
        var actual = await _cachingSut.HandleAsync(request, next, TestContext.Current.CancellationToken);

        // Assert
        actual.Should().WhichData.Should().Be("cached_value");
        await next.Received(1)();
        await VerifyCacheWasCalled("key_1", 1);
    }

    [Fact]
    public async Task HandleAsync_WhenCacheHit_ShouldReturnCachedValueWithoutCallingNext()
    {
        // Arrange
        var request = new TestCachedRequest("key_2");
        var cachedValue = Result.Success("cached_value");

        SetupCacheHit("key_2", cachedValue);

        var next = Substitute.For<RequestHandlerDelegate<Result<string>>>();

        // Act
        var actual = await _cachingSut.HandleAsync(request, next, TestContext.Current.CancellationToken);

        // Assert
        actual.Should().WhichData.Should().Be("cached_value");
        await next.DidNotReceive()();
        await VerifyCacheWasCalled("key_2", 1);
    }

    [Fact]
    public async Task HandleAsync_WhenRequestFails_ShouldReturnFailure()
    {
        // Arrange
        var request = new TestCachedRequest("key_error");
        var next = Substitute.For<RequestHandlerDelegate<Result<string>>>();
        var expected = Result.Failure<string>(Error.Failure("Code", "Message"));
        next().Returns(expected);

        SetupCacheMiss("key_error");

        // Act
        var actual = await _cachingSut.HandleAsync(request, next, TestContext.Current.CancellationToken);

        // Assert
        actual.Should().BeFailure();
        await next.Received(1)();
        await VerifyCacheWasCalled("key_error", 1);
    }

    [Fact]
    public async Task HandleAsync_WhenSubsequentRequestAfterFailure_ShouldCallNextAgain()
    {
        // Arrange
        var request = new TestCachedRequest("key_error");
        var next = Substitute.For<RequestHandlerDelegate<Result<string>>>();
        var expectedFailure = Result.Failure<string>(Error.Failure("Code", "Message"));
        var expectedSuccess = Result.Success("success_after_failure");
        next().Returns(expectedFailure, expectedSuccess);

        SetupCacheMiss("key_error");

        // Act
        var actual1 = await _cachingSut.HandleAsync(request, next, TestContext.Current.CancellationToken);
        var actual2 = await _cachingSut.HandleAsync(request, next, TestContext.Current.CancellationToken);

        // Assert
        actual1.Should().BeFailure();
        actual2.Should().WhichData.Should().Be("success_after_failure");
        await next.Received(2)();
        await VerifyCacheWasCalled("key_error", 2);
    }

    private void SetupCacheMiss(string cacheKey) =>
        _cache.GetOrCreateAsync<Func<CancellationToken, ValueTask<Result<string>>>, Result<string>>(
            cacheKey,
            Arg.Any<Func<CancellationToken, ValueTask<Result<string>>>>(),
            Arg.Any<Func<Func<CancellationToken, ValueTask<Result<string>>>, CancellationToken, ValueTask<Result<string>>>>(),
            Arg.Any<HybridCacheEntryOptions>(),
            Arg.Any<IEnumerable<string>>(),
            Arg.Any<CancellationToken>())
            .Returns(args =>
            {
                var state = (Func<CancellationToken, ValueTask<Result<string>>>)args[1]!;
                var factory = (Func<Func<CancellationToken, ValueTask<Result<string>>>, CancellationToken, ValueTask<Result<string>>>)args[2]!;
                var ct = (CancellationToken)args[5]!;
                return factory(state, ct);
            });

    private void SetupCacheHit(string cacheKey, Result<string> cachedValue) =>
        _cache.GetOrCreateAsync<Func<CancellationToken, ValueTask<Result<string>>>, Result<string>>(
            cacheKey,
            Arg.Any<Func<CancellationToken, ValueTask<Result<string>>>>(),
            Arg.Any<Func<Func<CancellationToken, ValueTask<Result<string>>>, CancellationToken, ValueTask<Result<string>>>>(),
            Arg.Any<HybridCacheEntryOptions>(),
            Arg.Any<IEnumerable<string>>(),
            Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(cachedValue));

    private async Task VerifyCacheWasCalled(string cacheKey, int times) =>
        await _cache.Received(times).GetOrCreateAsync<Func<CancellationToken, ValueTask<Result<string>>>, Result<string>>(
            cacheKey,
            Arg.Any<Func<CancellationToken, ValueTask<Result<string>>>>(),
            Arg.Any<Func<Func<CancellationToken, ValueTask<Result<string>>>, CancellationToken, ValueTask<Result<string>>>>(),
            Arg.Any<HybridCacheEntryOptions>(),
            Arg.Any<IEnumerable<string>>(),
            Arg.Any<CancellationToken>());

    private async Task VerifyCacheWasNotCalled() =>
        await _cache.DidNotReceiveWithAnyArgs().GetOrCreateAsync<Func<CancellationToken, ValueTask<Result<string>>>, Result<string>>(
            default!,
            default!,
            default!,
            default!,
            default!,
            default);

    public sealed record TestCachedRequest(string CacheKey, TimeSpan? Expiration = null) : IRequest<Result<string>>, ICachedQuery;
}
