namespace FB.Shared.Contracts;

public sealed record ReplyCommand
{
    public int SchemaVersion { get; init; } = 1;
    public string CommandId { get; init; } = string.Empty;
    public string EventId { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public ReplyTarget Target { get; init; } = new();
    public string? ReplyText { get; init; }
    public string? Intent { get; init; }
    public string? Sentiment { get; init; }
    public string? Reason { get; init; }
    public bool RequiresManualReview { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record ReplyTarget
{
    public string PageId { get; init; } = string.Empty;
    public string? CommentId { get; init; }
    public string? PostId { get; init; }
}
