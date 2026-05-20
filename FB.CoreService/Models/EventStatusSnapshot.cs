namespace FB.CoreService.Models;

public sealed record EventStatusSnapshot(
    string EventId,
    string State,
    string? Intent,
    string? Sentiment,
    string? Detail,
    DateTimeOffset UpdatedAt);
