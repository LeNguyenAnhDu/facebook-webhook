namespace FB.Shared.Contracts;

public static class ProcessingState
{
    public const string Received = "received";
    public const string Processing = "processing";
    public const string PendingReview = "pending_review";
    public const string Processed = "processed";
    public const string Replied = "replied";
    public const string Failed = "failed";
    public const string DeadLettered = "dead_lettered";
}
