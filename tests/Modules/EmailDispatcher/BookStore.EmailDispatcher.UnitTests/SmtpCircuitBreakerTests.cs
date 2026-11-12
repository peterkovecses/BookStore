namespace BookStore.EmailDispatcher.UnitTests;

public sealed class SmtpCircuitBreakerTests
{
    [Fact]
    public void Complete_WhenCircuitOpensAndProbeRecovers_ShouldLogOnlyTransitions()
    {
        // Arrange
        var clock = new FakeTimeProvider();
        var logs = new RecordingLoggerProvider();
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddProvider(logs).SetMinimumLevel(LogLevel.Trace));
        var settings = Options.Create(new SmtpSettings { CircuitFailureThreshold = 1, CircuitBreakSeconds = 30 });
        var sut = new SmtpCircuitBreaker(settings, clock, loggerFactory.CreateLogger<SmtpCircuitBreaker>());

        // Act
        sut.Complete(sut.Enter(), true, false);
        clock.Advance(TimeSpan.FromSeconds(30));
        sut.Complete(sut.Enter(), true, false);
        clock.Advance(TimeSpan.FromSeconds(30));
        sut.Complete(sut.Enter(), false, false);

        // Assert
        logs.Messages.Should().ContainSingle(log => log.Contains("circuit opened after"));
        logs.Messages.Should().ContainSingle(log => log.Contains("circuit reopened after"));
        logs.Messages.Should().ContainSingle(log => log.Contains("circuit recovered after"));
    }

    [Fact]
    public void Enter_WhenThresholdReached_ShouldOpenAndAllowOnlyOneRecoveryProbe()
    {
        // Arrange
        var clock = new FakeTimeProvider();
        var sut = new SmtpCircuitBreaker(Options.Create(new SmtpSettings { CircuitFailureThreshold = 2, CircuitBreakSeconds = 30 }), clock,
            NullLogger<SmtpCircuitBreaker>.Instance);
        sut.Complete(sut.Enter(), true, false);
        sut.Complete(sut.Enter(), true, false);

        // Act
        var actual = sut.GetAvailability();
        var act = () => sut.Enter();

        // Assert
        actual.IsAvailable.Should().BeFalse();
        actual.RetryAfter.Should().Be(clock.GetUtcNow().AddSeconds(30));
        act.Should().Throw<EmailCircuitOpenException>();
        clock.Advance(TimeSpan.FromSeconds(30));
        var probe = sut.Enter();
        act.Should().Throw<EmailCircuitOpenException>();
        sut.Complete(probe, false, false);
        sut.GetAvailability().IsAvailable.Should().BeTrue();
    }

    [Fact]
    public void Complete_WhenProbeFails_ShouldReopenCircuit()
    {
        // Arrange
        var clock = new FakeTimeProvider();
        var sut = new SmtpCircuitBreaker(Options.Create(new SmtpSettings { CircuitFailureThreshold = 1, CircuitBreakSeconds = 30 }), clock,
            NullLogger<SmtpCircuitBreaker>.Instance);
        sut.Complete(sut.Enter(), true, false);
        clock.Advance(TimeSpan.FromSeconds(30));

        // Act
        sut.Complete(sut.Enter(), true, false);

        // Assert
        sut.GetAvailability().RetryAfter.Should().Be(clock.GetUtcNow().AddSeconds(30));
    }

    [Fact]
    public void Complete_WhenProbeCancelled_ShouldPermitAnotherProbe()
    {
        // Arrange
        var clock = new FakeTimeProvider();
        var sut = new SmtpCircuitBreaker(Options.Create(new SmtpSettings { CircuitFailureThreshold = 1, CircuitBreakSeconds = 30 }), clock,
            NullLogger<SmtpCircuitBreaker>.Instance);
        sut.Complete(sut.Enter(), true, false);
        clock.Advance(TimeSpan.FromSeconds(30));
        var probe = sut.Enter();

        // Act
        sut.Complete(probe, false, true);

        // Assert
        sut.GetAvailability().IsAvailable.Should().BeTrue();
        sut.Enter().Should().BeTrue();
    }

    private sealed class RecordingLoggerProvider : ILoggerProvider
    {
        public ConcurrentQueue<string> Messages { get; } = new();

        public ILogger CreateLogger(string categoryName) => new RecordingLogger(Messages);

        public void Dispose() { }

        private sealed class RecordingLogger(ConcurrentQueue<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter) => messages.Enqueue(formatter(state, exception));
        }
    }
}
