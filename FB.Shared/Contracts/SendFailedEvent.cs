namespace FB.Shared.Contracts;

public sealed record SendFailedEvent
{
    public int SchemaVersion { get; init; } = 1;
    public string CommandId { get; init; } = string.Empty;
    public string EventId { get; init; } = string.Empty;
    public int RetryCount { get; init; }
    public string ErrorCode { get; init; } = string.Empty;
    public string LastError { get; init; } = string.Empty;
    public bool IsRetryable { get; init; } = true;
    public DateTimeOffset NextRetryAt { get; init; } = DateTimeOffset.UtcNow;
    public FailedPayload Payload { get; init; } = new();
}

public sealed record FailedPayload
{
    public string Action { get; init; } = string.Empty;
    public ReplyTarget Target { get; init; } = new();
    public string? ReplyText { get; init; }
    public string? Intent { get; init; }
    public string? Sentiment { get; init; }
}
