using System.Text.Json;
using FB.Shared.Contracts;

namespace FB.WebhookService.Services;

public sealed class FacebookWebhookEventNormalizer : IFacebookWebhookEventNormalizer
{
    public IReadOnlyList<RawEvent> Normalize(string payload)
    {
        using var document = JsonDocument.Parse(payload);
        var events = new List<RawEvent>();

        if (!document.RootElement.TryGetProperty("entry", out var entries) || entries.ValueKind != JsonValueKind.Array)
        {
            return events;
        }

        foreach (var entry in entries.EnumerateArray())
        {
            var pageId = entry.TryGetProperty("id", out var entryId) ? entryId.GetString() ?? string.Empty : string.Empty;

            if (entry.TryGetProperty("changes", out var changes) && changes.ValueKind == JsonValueKind.Array)
            {
                foreach (var change in changes.EnumerateArray())
                {
                    if (!change.TryGetProperty("value", out var value))
                    {
                        continue;
                    }

                    var item = value.TryGetProperty("item", out var itemNode) ? itemNode.GetString() : null;
                    if (!string.Equals(item, "comment", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    events.Add(new RawEvent
                    {
                        EventId = value.TryGetProperty("comment_id", out var commentIdNode)
                            ? commentIdNode.GetString() ?? Guid.NewGuid().ToString("N")
                            : Guid.NewGuid().ToString("N"),
                        EventType = "comment_created",
                        PageId = pageId,
                        PostId = value.TryGetProperty("post_id", out var postIdNode) ? postIdNode.GetString() : null,
                        CommentId = value.TryGetProperty("comment_id", out commentIdNode) ? commentIdNode.GetString() : null,
                        UserId = value.TryGetProperty("from", out var fromNode) && fromNode.TryGetProperty("id", out var userIdNode)
                            ? userIdNode.GetString()
                            : null,
                        Message = value.TryGetProperty("message", out var messageNode) ? messageNode.GetString() : null,
                        CreatedAt = GetTimestamp(value),
                        OriginalPayload = JsonSerializer.Deserialize<object>(change.GetRawText())
                    });
                }
            }

            if (entry.TryGetProperty("messaging", out var messaging) && messaging.ValueKind == JsonValueKind.Array)
            {
                foreach (var messageEvent in messaging.EnumerateArray())
                {
                    if (!messageEvent.TryGetProperty("message", out var messageNode) ||
                        !messageNode.TryGetProperty("text", out var textNode))
                    {
                        continue;
                    }

                    events.Add(new RawEvent
                    {
                        EventId = messageNode.TryGetProperty("mid", out var midNode)
                            ? midNode.GetString() ?? Guid.NewGuid().ToString("N")
                            : Guid.NewGuid().ToString("N"),
                        EventType = "message_created",
                        PageId = pageId,
                        UserId = messageEvent.TryGetProperty("sender", out var senderNode) && senderNode.TryGetProperty("id", out var senderIdNode)
                            ? senderIdNode.GetString()
                            : null,
                        Message = textNode.GetString(),
                        CreatedAt = messageEvent.TryGetProperty("timestamp", out var tsNode) && tsNode.TryGetInt64(out var timestamp)
                            ? DateTimeOffset.FromUnixTimeMilliseconds(timestamp)
                            : DateTimeOffset.UtcNow,
                        OriginalPayload = JsonSerializer.Deserialize<object>(messageEvent.GetRawText())
                    });
                }
            }
        }

        return events;
    }

    private static DateTimeOffset GetTimestamp(JsonElement value)
    {
        if (value.TryGetProperty("created_time", out var createdTimeNode) &&
            DateTimeOffset.TryParse(createdTimeNode.GetString(), out var createdAt))
        {
            return createdAt;
        }

        if (value.TryGetProperty("published", out var publishedNode) && publishedNode.TryGetInt64(out var timestamp))
        {
            return DateTimeOffset.FromUnixTimeSeconds(timestamp);
        }

        return DateTimeOffset.UtcNow;
    }
}
