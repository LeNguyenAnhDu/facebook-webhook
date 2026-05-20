using FB.CoreService.Options;
using Microsoft.Extensions.Options;

namespace FB.CoreService.Services;

public sealed class InMemoryAiCircuitBreaker : IAiCircuitBreaker
{
    private readonly AiClassificationOptions _options;
    private readonly object _lock = new();
    private int _consecutiveFailures;
    private DateTimeOffset? _openUntil;
    private bool _halfOpenProbeIssued;

    public InMemoryAiCircuitBreaker(IOptions<AiClassificationOptions> options)
    {
        _options = options.Value;
    }

    public bool AllowRequest()
    {
        lock (_lock)
        {
            if (_openUntil.HasValue && _openUntil.Value > DateTimeOffset.UtcNow)
            {
                return false;
            }

            if (_openUntil.HasValue && _openUntil.Value <= DateTimeOffset.UtcNow)
            {
                if (_halfOpenProbeIssued)
                {
                    return false;
                }

                _halfOpenProbeIssued = true;
                return true;
            }

            return true;
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

    public void RecordFailure()
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
