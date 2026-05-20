using FB.BackendAPI.Options;
using Microsoft.Extensions.Options;

namespace FB.BackendAPI.Services;

public sealed class InMemoryFacebookCircuitBreaker : IFacebookCircuitBreaker
{
    private readonly CircuitBreakerOptions _options;
    private readonly object _lock = new();
    private int _consecutiveFailures;
    private DateTimeOffset? _openUntil;
    private bool _halfOpenProbeIssued;

    public InMemoryFacebookCircuitBreaker(IOptions<CircuitBreakerOptions> options)
    {
        _options = options.Value;
    }

    public void ThrowIfOpen()
    {
        lock (_lock)
        {
            if (_openUntil.HasValue && _openUntil.Value > DateTimeOffset.UtcNow)
            {
                throw new FacebookApiException(
                    "facebook_circuit_open",
                    "Facebook downstream circuit breaker is open.",
                    StatusCodes.Status503ServiceUnavailable,
                    true,
                    $"Retry after {_openUntil.Value:O}");
            }

            if (_openUntil.HasValue && _openUntil.Value <= DateTimeOffset.UtcNow)
            {
                if (_halfOpenProbeIssued)
                {
                    throw new FacebookApiException(
                        "facebook_circuit_half_open_busy",
                        "Facebook downstream circuit breaker is half-open and a probe is already running.",
                        StatusCodes.Status503ServiceUnavailable,
                        true,
                        $"Retry after {_openUntil.Value:O}");
                }

                _halfOpenProbeIssued = true;
            }
        }
    }

    public void RecordSuccess()
    {
        lock (_lock)
        {
            _consecutiveFailures = 0;
            _openUntil = null;
            _halfOpenProbeIssued = false;
        }
    }

    public void RecordFailure(string? detail = null)
    {
        lock (_lock)
        {
            _consecutiveFailures++;
            _halfOpenProbeIssued = false;
            if (_consecutiveFailures >= _options.FailureThreshold)
            {
                _openUntil = DateTimeOffset.UtcNow.AddSeconds(_options.BreakDurationSeconds);
            }
        }
    }
}
