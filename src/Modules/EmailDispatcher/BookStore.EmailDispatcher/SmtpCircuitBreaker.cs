using BookStore.EmailDispatcher.Contracts;
using Microsoft.Extensions.Options;

namespace BookStore.EmailDispatcher;

internal sealed class SmtpCircuitBreaker(IOptions<SmtpSettings> options, TimeProvider timeProvider) : IEmailDispatchAvailability
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
        lock (_sync)
        {
            if (!_settings.CircuitEnabled) return;
            if (probe)
            {
                _probe = false;
                if (cancelled) return;
                if (transientFailure)
                {
                    _until = timeProvider.GetUtcNow().AddSeconds(_settings.CircuitBreakSeconds);
                    return;
                }
                _until = null;
                _failures = 0;
                return;
            }
            if (cancelled || _until is not null) return;
            if (!transientFailure)
            {
                _failures = 0;
                return;
            }
            if (++_failures >= _settings.CircuitFailureThreshold)
                _until = timeProvider.GetUtcNow().AddSeconds(_settings.CircuitBreakSeconds);
        }
    }
}
