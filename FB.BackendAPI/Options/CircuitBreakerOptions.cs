namespace FB.BackendAPI.Options;

public sealed class CircuitBreakerOptions
{
    public const string SectionName = "CircuitBreaker";

    public int FailureThreshold { get; set; } = 10;

    public int BreakDurationSeconds { get; set; } = 30;
}
