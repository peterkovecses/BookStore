namespace BookStore.EmailDispatcher.Infrastructure.Smtp;

internal sealed class SmtpCircuitBreaker(IOptions<SmtpSettings> options, TimeProvider timeProvider,
    ILogger<SmtpCircuitBreaker> logger) : IEmailDispatchAvailability
{
    private readonly SmtpSettings _settings = options.Value;
    private readonly Lock _sync = new();
    private int _failures;
    private DateTimeOffset? _until;
    private bool _probe;

    public EmailDispatchAvailability GetAvailability()
    {
        lock (_sync)
        {
            var available = !_settings.CircuitEnabled || _until is null || (_until <= timeProvider.GetUtcNow() && !_probe);

            return new(available, available || _probe ? null : _until);
        }
    }

    public bool Enter()
    {
        lock (_sync)
        {
            if (!_settings.CircuitEnabled || _until is null) return false;
            if (_until > timeProvider.GetUtcNow() || _probe) throw new EmailCircuitOpenException(_probe ? null : _until);
            _probe = true;

            return true;
        }
    }

    public void Complete(bool probe, bool transientFailure, bool cancelled)
    {
        (string Transition, int Failures, DateTimeOffset? RetryAfter)? transition;
        lock (_sync)
        {
            transition = UpdateState(probe, transientFailure, cancelled);
        }

        if (transition is { } result) logger.Transition(result.Transition, result.Failures, result.RetryAfter);
    }

    private (string Transition, int Failures, DateTimeOffset? RetryAfter)? UpdateState(
        bool probe, bool transientFailure, bool cancelled)
    {
        if (!_settings.CircuitEnabled) return null;
        if (probe)
        {
            _probe = false;
            if (cancelled) return null;
            if (transientFailure)
            {
                _until = timeProvider.GetUtcNow().AddSeconds(_settings.CircuitBreakSeconds);

                return ("reopened", _failures, _until);
            }

            _until = null;
            _failures = 0;

            return ("recovered", 0, null);
        }

        if (cancelled || _until is not null) return null;
        if (!transientFailure)
        {
            _failures = 0;

            return null;
        }

        var failures = ++_failures;
        if (failures < _settings.CircuitFailureThreshold) return null;
        _until = timeProvider.GetUtcNow().AddSeconds(_settings.CircuitBreakSeconds);

        return ("opened", failures, _until);
    }
}

internal static partial class SmtpCircuitBreakerLogging
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "SMTP circuit {Transition} after {Failures} consecutive transient failures until {RetryAfter}")]
    internal static partial void Transition(this ILogger<SmtpCircuitBreaker> logger, string transition, int failures, DateTimeOffset? retryAfter);
}
