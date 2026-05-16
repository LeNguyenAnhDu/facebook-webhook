using System.Text.Json.Serialization;

namespace FB.BackendAPI.Models;

public sealed record GetPostsRequest(int Limit = 10);

public sealed record CreatePostRequest(string Message);

public sealed record ReplyToCommentRequest(string Message, string? CommandId = null, string? EventId = null);

public sealed record HideCommentRequest(bool IsHidden = true, string? CommandId = null, string? EventId = null);

public sealed record FacebookPostSummary(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("message")] string? Message,
    [property: JsonPropertyName("created_time")] string? CreatedTime,
    [property: JsonPropertyName("permalink_url")] string? PermalinkUrl);

public sealed record FacebookPagingCursor(
    [property: JsonPropertyName("before")] string? Before,
    [property: JsonPropertyName("after")] string? After);

public sealed record FacebookPaging(
    [property: JsonPropertyName("cursors")] FacebookPagingCursor? Cursors,
    [property: JsonPropertyName("next")] string? Next);

public sealed record FacebookListResponse<T>(
    [property: JsonPropertyName("data")] IReadOnlyList<T> Data,
    [property: JsonPropertyName("paging")] FacebookPaging? Paging);

public sealed record FacebookMutationResponse(
    [property: JsonPropertyName("id")] string? Id,
    [property: JsonPropertyName("success")] bool? Success);
