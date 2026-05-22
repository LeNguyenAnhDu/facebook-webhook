using FB.BackendAPI.Models;
using FB.Shared.Constants;
using FB.Shared.Contracts;
using FB.Shared.Kafka;

namespace FB.BackendAPI.Services;

public sealed class FacebookCommandService : IFacebookCommandService
{
    private readonly IFacebookGraphService _facebookGraphService;
    private readonly ICommandIdempotencyStore _idempotencyStore;
    private readonly ICommentStatusRepository _commentStatusRepository;
    private readonly IKafkaProducer _kafkaProducer;
    private readonly ILogger<FacebookCommandService> _logger;

    public FacebookCommandService(
        IFacebookGraphService facebookGraphService,
        ICommandIdempotencyStore idempotencyStore,
        ICommentStatusRepository commentStatusRepository,
        IKafkaProducer kafkaProducer,
        ILogger<FacebookCommandService> logger)
    {
        _facebookGraphService = facebookGraphService;
        _idempotencyStore = idempotencyStore;
        _commentStatusRepository = commentStatusRepository;
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
            new ReplyTarget { CommentId = commentId },
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
            new ReplyTarget { CommentId = commentId },
            null,
            () => _facebookGraphService.SetCommentHiddenAsync(commentId, request.IsHidden, cancellationToken),
            cancellationToken);
    }

    private async Task<FacebookMutationResponse> ExecuteWithFailurePublishingAsync(
        string commandId,
        string eventId,
        string action,
        ReplyTarget target,
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
            await _commentStatusRepository.UpdateStatusAsync(
                target.CommentId,
                action == "reply" ? "replied" : "hidden",
                cancellationToken);
            return response;
        }
        catch (Exception exception)
        {
            var retryableException = exception as FacebookApiException;
            await _commentStatusRepository.UpdateStatusAsync(target.CommentId, "failed", cancellationToken);
            var failedEvent = new SendFailedEvent
            {
                CommandId = commandId,
                EventId = eventId,
                RetryCount = 0,
                ErrorCode = retryableException?.ErrorCode ?? "unexpected_error",
                LastError = exception.Message,
                IsRetryable = retryableException?.IsRetryable ?? true,
                NextRetryAt = DateTimeOffset.UtcNow.AddSeconds(1),
                Payload = new FailedPayload
                {
                    Action = action,
                    Target = target,
                    ReplyText = replyText
                }
            };

            await _kafkaProducer.ProduceAsync(KafkaTopics.SendFailed, failedEvent, cancellationToken);
            throw;
        }
    }
}
