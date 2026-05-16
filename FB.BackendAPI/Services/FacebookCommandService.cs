using FB.BackendAPI.Models;
using FB.Shared.Constants;
using FB.Shared.Contracts;
using FB.Shared.Kafka;

namespace FB.BackendAPI.Services;

public sealed class FacebookCommandService : IFacebookCommandService
{
    private readonly IFacebookGraphService _facebookGraphService;
    private readonly ICommandIdempotencyStore _idempotencyStore;
    private readonly IKafkaProducer _kafkaProducer;
    private readonly ILogger<FacebookCommandService> _logger;

    public FacebookCommandService(
        IFacebookGraphService facebookGraphService,
        ICommandIdempotencyStore idempotencyStore,
        IKafkaProducer kafkaProducer,
        ILogger<FacebookCommandService> logger)
    {
        _facebookGraphService = facebookGraphService;
        _idempotencyStore = idempotencyStore;
        _kafkaProducer = kafkaProducer;
        _logger = logger;
    }

    public Task<FacebookMutationResponse> ReplyToCommentAsync(string commentId, ReplyToCommentRequest request, CancellationToken cancellationToken)
    {
        var commandId = request.CommandId ?? Guid.NewGuid().ToString("N");
        var eventId = request.EventId ?? $"manual_{Guid.NewGuid():N}";

        return ExecuteWithFailurePublishingAsync(
            commandId,
            eventId,
            "reply",
            request.Message,
            () => _facebookGraphService.ReplyToCommentAsync(commentId, request.Message, cancellationToken),
            cancellationToken);
    }

    public Task<FacebookMutationResponse> HideCommentAsync(string commentId, HideCommentRequest request, CancellationToken cancellationToken)
    {
        var commandId = request.CommandId ?? Guid.NewGuid().ToString("N");
        var eventId = request.EventId ?? $"manual_{Guid.NewGuid():N}";

        return ExecuteWithFailurePublishingAsync(
            commandId,
            eventId,
            "hide_comment",
            null,
            () => _facebookGraphService.SetCommentHiddenAsync(commentId, request.IsHidden, cancellationToken),
            cancellationToken);
    }

    private async Task<FacebookMutationResponse> ExecuteWithFailurePublishingAsync(
        string commandId,
        string eventId,
        string action,
        string? replyText,
        Func<Task<FacebookMutationResponse>> operation,
        CancellationToken cancellationToken)
    {
        if (await _idempotencyStore.HasProcessedAsync(commandId, cancellationToken))
        {
            _logger.LogInformation("Skipped duplicate command {CommandId}", commandId);
            return new FacebookMutationResponse(null, true);
        }

        try
        {
            var response = await operation();
            await _idempotencyStore.MarkProcessedAsync(commandId, cancellationToken);
            return response;
        }
        catch (Exception exception)
        {
            var failedEvent = new SendFailedEvent
            {
                CommandId = commandId,
                EventId = eventId,
                RetryCount = 0,
                LastError = exception.Message,
                NextRetryAt = DateTimeOffset.UtcNow.AddSeconds(1),
                Payload = new FailedPayload
                {
                    Action = action,
                    ReplyText = replyText
                }
            };

            await _kafkaProducer.ProduceAsync(KafkaTopics.SendFailed, failedEvent, cancellationToken);
            throw;
        }
    }
}
