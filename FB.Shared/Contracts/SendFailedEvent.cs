namespace FB.Shared.Contracts;

public sealed record SendFailedEvent
{
    public int SchemaVersion { get; init; } = 1;
    public string CommandId { get; init; } = string.Empty;
    public string EventId { get; init; } = string.Empty;
    public int RetryCount { get; init; }
    public string LastError { get; init; } = string.Empty;
    public DateTimeOffset NextRetryAt { get; init; } = DateTimeOffset.UtcNow;
    public FailedPayload Payload { get; init; } = new();
}

public sealed record FailedPayload
{
    public string Action { get; init; } = string.Empty;
    public string? ReplyText { get; init; }
}
