namespace FB.Shared.Contracts;

public sealed record RawEvent
{
    public int SchemaVersion { get; init; } = 1;
    public string EventId { get; init; } = string.Empty;
    public string EventType { get; init; } = string.Empty;
    public string Source { get; init; } = "facebook";
    public string PageId { get; init; } = string.Empty;
    public string? PostId { get; init; }
    public string? CommentId { get; init; }
    public string? UserId { get; init; }
    public string? Message { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public object? OriginalPayload { get; init; }
}
