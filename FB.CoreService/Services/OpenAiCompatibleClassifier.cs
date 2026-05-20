using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FB.CoreService.Models;
using FB.CoreService.Options;
using FB.Shared.Contracts;
using Microsoft.Extensions.Options;

namespace FB.CoreService.Services;

public sealed class OpenAiCompatibleClassifier : IAiClassifier
{
    private readonly HttpClient _httpClient;
    private readonly AiClassificationOptions _options;
    private readonly IAiCircuitBreaker _circuitBreaker;
    private readonly IAiClassifierFallback _fallback;
    private readonly ILogger<OpenAiCompatibleClassifier> _logger;

    public OpenAiCompatibleClassifier(
        HttpClient httpClient,
        IOptions<AiClassificationOptions> options,
        IAiCircuitBreaker circuitBreaker,
        IAiClassifierFallback fallback,
        ILogger<OpenAiCompatibleClassifier> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _circuitBreaker = circuitBreaker;
        _fallback = fallback;
        _logger = logger;
        _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
    }

    public async Task<AiClassificationResult> ClassifyAsync(RawEvent rawEvent, CancellationToken cancellationToken)
    {
        if (!string.Equals(_options.Provider, "openai-compatible", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(_options.Endpoint) ||
            string.IsNullOrWhiteSpace(_options.ApiKey) ||
            string.IsNullOrWhiteSpace(_options.Model))
        {
            return _fallback.Classify(rawEvent);
        }

        if (!_circuitBreaker.AllowRequest())
        {
            _logger.LogWarning("AI circuit breaker is open. Falling back without calling external AI.");
            var fallbackResult = _fallback.Classify(rawEvent);
            return fallbackResult with { Summary = "AI circuit breaker open, heuristic fallback used.", UsedFallback = true };
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, _options.Endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

            var body = new
            {
                model = _options.Model,
                response_format = new { type = "json_object" },
                messages = new object[]
                {
                    new
                    {
                        role = "system",
                        content = "Classify a Facebook comment/message. Return strict JSON with keys intent, sentiment, summary. sentiment must be one of positive, neutral, negative. intent should be one of ask_price, support_request, praise, general_engagement, spam."
                    },
                    new
                    {
                        role = "user",
                        content = $"event_type: {rawEvent.EventType}\nmessage: {rawEvent.Message}"
                    }
                }
            };

            request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var payload = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _circuitBreaker.RecordFailure();
                _logger.LogWarning("AI classifier failed with status {StatusCode}. Falling back.", (int)response.StatusCode);
                return _fallback.Classify(rawEvent);
            }

            using var document = JsonDocument.Parse(payload);
            var content = document.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            if (string.IsNullOrWhiteSpace(content))
            {
                _circuitBreaker.RecordFailure();
                return _fallback.Classify(rawEvent);
            }

            using var contentDoc = JsonDocument.Parse(content);
            var result = new AiClassificationResult(
                contentDoc.RootElement.GetProperty("intent").GetString() ?? "general_engagement",
                contentDoc.RootElement.GetProperty("sentiment").GetString() ?? "neutral",
                contentDoc.RootElement.GetProperty("summary").GetString() ?? "AI classified event.",
                false);
            _circuitBreaker.RecordSuccess();
            return result;
        }
        catch (Exception exception)
        {
            _circuitBreaker.RecordFailure();
            _logger.LogWarning(exception, "AI classifier threw an exception. Falling back.");
            return _fallback.Classify(rawEvent);
        }
    }
}
