using System.Text;
using FB.Shared.Api;
using FB.Shared.Constants;
using FB.Shared.Kafka;
using FB.WebhookService.Options;
using FB.WebhookService.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace FB.WebhookService.Controllers;

[ApiController]
[Route("webhook")]
public sealed class FacebookWebhookController : ControllerBase
{
    private readonly FacebookWebhookOptions _options;
    private readonly IFacebookWebhookSignatureValidator _signatureValidator;
    private readonly IFacebookWebhookEventNormalizer _eventNormalizer;
    private readonly IKafkaProducer _kafkaProducer;
    private readonly ILogger<FacebookWebhookController> _logger;

    public FacebookWebhookController(
        IOptions<FacebookWebhookOptions> options,
        IFacebookWebhookSignatureValidator signatureValidator,
        IFacebookWebhookEventNormalizer eventNormalizer,
        IKafkaProducer kafkaProducer,
        ILogger<FacebookWebhookController> logger)
    {
        _options = options.Value;
        _signatureValidator = signatureValidator;
        _eventNormalizer = eventNormalizer;
        _kafkaProducer = kafkaProducer;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Verify(
        [FromQuery(Name = "hub.mode")] string? mode,
        [FromQuery(Name = "hub.verify_token")] string? verifyToken,
        [FromQuery(Name = "hub.challenge")] string? challenge)
    {
        if (string.Equals(mode, "subscribe", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(verifyToken, _options.VerifyToken, StringComparison.Ordinal))
        {
            return Content(challenge ?? string.Empty, "text/plain", Encoding.UTF8);
        }

        return Unauthorized(ApiResponse<object>.Fail("facebook_webhook_verification_failed", "Webhook verify token is invalid."));
    }

    [HttpPost]
    public async Task<IActionResult> Receive(CancellationToken cancellationToken)
    {
        Request.EnableBuffering();

        using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
        var payload = await reader.ReadToEndAsync(cancellationToken);
        Request.Body.Position = 0;

        var signature = Request.Headers["X-Hub-Signature-256"].FirstOrDefault();
        if (!_signatureValidator.IsValid(payload, signature))
        {
            _logger.LogWarning("Rejected webhook request because the signature is invalid.");
            return Unauthorized(ApiResponse<object>.Fail("facebook_signature_invalid", "Webhook signature validation failed."));
        }

        var rawEvents = _eventNormalizer.Normalize(payload);
        foreach (var rawEvent in rawEvents)
        {
            await _kafkaProducer.ProduceAsync(KafkaTopics.RawEvents, rawEvent, cancellationToken);
        }

        _logger.LogInformation("Accepted webhook request and published {Count} events to Kafka.", rawEvents.Count);
        return Ok(ApiResponse<object>.Ok(new { published = rawEvents.Count }));
    }
}
