using System.Net;
using System.Text;
using System.Text.Json;
using FB.BackendAPI.Models;
using FB.BackendAPI.Options;
using Microsoft.Extensions.Options;

namespace FB.BackendAPI.Services;

public sealed class FacebookGraphService : IFacebookGraphService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    private readonly FacebookGraphOptions _options;
    private readonly ILogger<FacebookGraphService> _logger;

    public FacebookGraphService(HttpClient httpClient, IOptions<FacebookGraphOptions> options, ILogger<FacebookGraphService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public Task<FacebookListResponse<FacebookPostSummary>> GetPostsAsync(int limit, CancellationToken cancellationToken)
    {
        var pageId = GetDefaultPageId();
        var uri = $"{pageId}/posts?fields=id,message,created_time,permalink_url&limit={limit}&access_token={Uri.EscapeDataString(_options.PageAccessToken)}";
        return SendAsync<FacebookListResponse<FacebookPostSummary>>(HttpMethod.Get, uri, body: null, cancellationToken);
    }

    public Task<FacebookMutationResponse> CreatePostAsync(string message, CancellationToken cancellationToken)
    {
        var pageId = GetDefaultPageId();
        var body = new Dictionary<string, string>
        {
            ["message"] = message,
            ["access_token"] = _options.PageAccessToken
        };

        return SendAsync<FacebookMutationResponse>(HttpMethod.Post, $"{pageId}/feed", body, cancellationToken);
    }

    public Task<FacebookMutationResponse> ReplyToCommentAsync(string commentId, string message, CancellationToken cancellationToken)
    {
        var body = new Dictionary<string, string>
        {
            ["message"] = message,
            ["access_token"] = _options.PageAccessToken
        };

        return SendAsync<FacebookMutationResponse>(HttpMethod.Post, $"{commentId}/comments", body, cancellationToken);
    }

    public Task<FacebookMutationResponse> SetCommentHiddenAsync(string commentId, bool isHidden, CancellationToken cancellationToken)
    {
        var body = new Dictionary<string, string>
        {
            ["is_hidden"] = isHidden ? "true" : "false",
            ["access_token"] = _options.PageAccessToken
        };

        return SendAsync<FacebookMutationResponse>(HttpMethod.Post, commentId, body, cancellationToken);
    }

    private async Task<TResponse> SendAsync<TResponse>(
        HttpMethod method,
        string relativeUri,
        IReadOnlyDictionary<string, string>? body,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, relativeUri);
        if (body is not null)
        {
            request.Content = new FormUrlEncodedContent(body);
        }

        _logger.LogInformation(
            "Sending Facebook Graph request {Method} {Uri} at {Timestamp}. Payload preview: {Payload}",
            method.Method,
            relativeUri,
            DateTimeOffset.UtcNow,
            body is null ? "<none>" : string.Join(", ", body.Select(entry => $"{entry.Key}={Truncate(entry.Value)}")));

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);

        _logger.LogInformation(
            "Facebook Graph response {StatusCode} for {Method} {Uri}. Body preview: {Payload}",
            (int)response.StatusCode,
            method.Method,
            relativeUri,
            Truncate(payload, 500));

        if (!response.IsSuccessStatusCode)
        {
            throw BuildFacebookApiException(response.StatusCode, payload);
        }

        var result = JsonSerializer.Deserialize<TResponse>(payload, SerializerOptions);
        if (result is null)
        {
            throw new FacebookApiException("facebook_invalid_response", "Facebook returned an unreadable response.", StatusCodes.Status502BadGateway, payload);
        }

        return result;
    }

    private static FacebookApiException BuildFacebookApiException(HttpStatusCode statusCode, string payload)
    {
        using var document = JsonDocument.Parse(payload);
        var errorNode = document.RootElement.TryGetProperty("error", out var error) ? error : document.RootElement;

        var errorCode = errorNode.TryGetProperty("code", out var code) ? code.ToString() : "facebook_api_error";
        var message = errorNode.TryGetProperty("message", out var msg)
            ? msg.GetString() ?? "Facebook API request failed."
            : "Facebook API request failed.";

        var mappedStatus = statusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => StatusCodes.Status401Unauthorized,
            HttpStatusCode.BadRequest => StatusCodes.Status400BadRequest,
            HttpStatusCode.TooManyRequests => StatusCodes.Status429TooManyRequests,
            _ => StatusCodes.Status502BadGateway
        };

        return new FacebookApiException(errorCode, message, mappedStatus, payload);
    }

    private static string Truncate(string value, int maxLength = 120)
    {
        return value.Length <= maxLength ? value : $"{value[..maxLength]}...";
    }

    private string GetDefaultPageId()
    {
        if (string.IsNullOrWhiteSpace(_options.DefaultPageId))
        {
            throw new FacebookApiException(
                "facebook_page_id_missing",
                "Default Facebook Page ID is not configured.",
                StatusCodes.Status500InternalServerError,
                "Set FacebookGraph:DefaultPageId in configuration.");
        }

        return _options.DefaultPageId;
    }
}
