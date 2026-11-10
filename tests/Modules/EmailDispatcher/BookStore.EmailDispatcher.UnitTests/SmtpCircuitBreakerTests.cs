using BookStore.EmailDispatcher.Contracts;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace BookStore.EmailDispatcher.UnitTests;

public sealed class SmtpCircuitBreakerTests
{
    [Fact]
    public void Enter_WhenThresholdReached_ShouldOpenAndAllowOnlyOneRecoveryProbe()
    {
        // Arrange
        var clock = new FakeTimeProvider();
        var sut = new SmtpCircuitBreaker(Options.Create(new SmtpSettings { CircuitFailureThreshold = 2, CircuitBreakSeconds = 30 }), clock);
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
        var sut = new SmtpCircuitBreaker(Options.Create(new SmtpSettings { CircuitFailureThreshold = 1, CircuitBreakSeconds = 30 }), clock);
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
        var sut = new SmtpCircuitBreaker(Options.Create(new SmtpSettings { CircuitFailureThreshold = 1, CircuitBreakSeconds = 30 }), clock);
        sut.Complete(sut.Enter(), true, false);
        clock.Advance(TimeSpan.FromSeconds(30));
        var probe = sut.Enter();

        // Act
        sut.Complete(probe, false, true);

        // Assert
        sut.GetAvailability().IsAvailable.Should().BeTrue();
        sut.Enter().Should().BeTrue();
    }
}
