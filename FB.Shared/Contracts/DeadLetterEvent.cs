namespace FB.Shared.Contracts;

public sealed record DeadLetterEvent
{
    public int SchemaVersion { get; init; } = 1;
    public string CommandId { get; init; } = string.Empty;
    public string EventId { get; init; } = string.Empty;
    public int RetryCount { get; init; }
    public DateTimeOffset FailedAt { get; init; } = DateTimeOffset.UtcNow;
    public string FinalError { get; init; } = string.Empty;
    public string OriginalTopic { get; init; } = string.Empty;
    public FailedPayload Payload { get; init; } = new();
}
