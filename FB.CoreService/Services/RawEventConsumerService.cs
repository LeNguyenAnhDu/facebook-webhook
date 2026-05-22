using System.Text.Json;
using Confluent.Kafka;
using FB.CoreService.Models;
using FB.CoreService.Options;
using FB.Shared.Constants;
using FB.Shared.Contracts;
using FB.Shared.Kafka;
using Microsoft.Extensions.Options;

namespace FB.CoreService.Services;

public sealed class RawEventConsumerService : BackgroundService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly IServiceProvider _serviceProvider;
    private readonly KafkaOptions _kafkaOptions;
    private readonly KafkaConsumerOptions _consumerOptions;
    private readonly AutomationOptions _automationOptions;
    private readonly ILogger<RawEventConsumerService> _logger;

    public RawEventConsumerService(
        IServiceProvider serviceProvider,
        IOptions<KafkaOptions> kafkaOptions,
        IOptions<KafkaConsumerOptions> consumerOptions,
        IOptions<AutomationOptions> automationOptions,
        ILogger<RawEventConsumerService> logger)
    {
        _serviceProvider = serviceProvider;
        _kafkaOptions = kafkaOptions.Value;
        _consumerOptions = consumerOptions.Value;
        _automationOptions = automationOptions.Value;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return Task.Run(async () =>
        {
            var config = new ConsumerConfig
            {
                BootstrapServers = _kafkaOptions.BootstrapServers,
                GroupId = _consumerOptions.GroupId,
                EnableAutoCommit = false,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                AllowAutoCreateTopics = false,
                MaxPollIntervalMs = 300000
            };

            using var consumer = new ConsumerBuilder<Ignore, string>(config).Build();
            consumer.Subscribe(KafkaTopics.RawEvents);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var result = consumer.Consume(stoppingToken);
                    var rawEvent = JsonSerializer.Deserialize<RawEvent>(result.Message.Value, SerializerOptions);
                    if (rawEvent is null)
                    {
                        consumer.Commit(result);
                        continue;
                    }

                    using var scope = _serviceProvider.CreateScope();
                    var statusStore = scope.ServiceProvider.GetRequiredService<IEventProcessingStatusStore>();
                    if (!statusStore.MarkReceived(rawEvent.EventId))
                    {
                        _logger.LogInformation("Skipped duplicate raw event {EventId}", rawEvent.EventId);
                        consumer.Commit(result);
                        continue;
                    }

                    await ProcessAsync(scope.ServiceProvider, rawEvent, _automationOptions, stoppingToken);
                    consumer.Commit(result);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Failed while consuming raw_events.");
                    await Task.Delay(500, stoppingToken);
                }
            }
        }, stoppingToken);
    }

    private static async Task ProcessAsync(IServiceProvider serviceProvider, RawEvent rawEvent, AutomationOptions automationOptions, CancellationToken cancellationToken)
    {
        var statusStore = serviceProvider.GetRequiredService<IEventProcessingStatusStore>();
        var activityStore = serviceProvider.GetRequiredService<IUserActivityStore>();
        var spamDetector = serviceProvider.GetRequiredService<ISpamDetector>();
        var classifier = serviceProvider.GetRequiredService<IAiClassifier>();
        var ruleEngine = serviceProvider.GetRequiredService<IAutomationRuleEngine>();
        var commentRepository = serviceProvider.GetRequiredService<ICommentRepository>();
        var producer = serviceProvider.GetRequiredService<IKafkaProducer>();
        var logger = serviceProvider.GetRequiredService<ILogger<RawEventConsumerService>>();

        statusStore.Upsert(new EventStatusSnapshot(rawEvent.EventId, ProcessingState.Received, null, null, "Event received from raw_events.", DateTimeOffset.UtcNow));
        await commentRepository.UpsertReceivedAsync(rawEvent, cancellationToken);

        if (IsSelfGeneratedByPage(rawEvent))
        {
            statusStore.Upsert(new EventStatusSnapshot(rawEvent.EventId, ProcessingState.Processed, null, null, "Skipped automation because the comment was created by the page itself.", DateTimeOffset.UtcNow));
            await commentRepository.UpdateAnalysisAsync(rawEvent.CommentId, null, null, ProcessingState.Processed, cancellationToken);
            logger.LogInformation(
                "Skipped self-generated page event {EventId}. userId={UserId}, pageId={PageId}, commentId={CommentId}",
                rawEvent.EventId,
                rawEvent.UserId,
                rawEvent.PageId,
                rawEvent.CommentId);
            return;
        }

        var activity = activityStore.Track(rawEvent);
        if (activity.EventsLastMinute >= automationOptions.RateLimitPerMinute)
        {
            statusStore.Upsert(new EventStatusSnapshot(rawEvent.EventId, ProcessingState.PendingReview, null, null, "Rate limit triggered, pending review.", DateTimeOffset.UtcNow));
            await commentRepository.UpdateAnalysisAsync(rawEvent.CommentId, null, null, ProcessingState.PendingReview, cancellationToken);
            return;
        }

        statusStore.Upsert(new EventStatusSnapshot(rawEvent.EventId, ProcessingState.Processing, null, null, "Running spam detection and classification.", DateTimeOffset.UtcNow));

        var spamResult = spamDetector.Detect(rawEvent, activity);
        var classification = await classifier.ClassifyAsync(rawEvent, cancellationToken);
        var decision = ruleEngine.Evaluate(rawEvent, classification, spamResult, activity);
        logger.LogInformation(
            "Processed raw event {EventId}. intent={Intent}, sentiment={Sentiment}, state={State}, commands={CommandCount}, reason={Reason}",
            rawEvent.EventId,
            classification.Intent,
            classification.Sentiment,
            decision.State,
            decision.Commands.Count,
            decision.Reason);

        foreach (var command in decision.Commands)
        {
            await producer.ProduceAsync(KafkaTopics.ReplyCommands, command, cancellationToken);
            logger.LogInformation(
                "Published reply command {CommandId} for event {EventId}. action={Action}, sentiment={Sentiment}",
                command.CommandId,
                command.EventId,
                command.Action,
                command.Sentiment);
        }

        statusStore.Upsert(new EventStatusSnapshot(rawEvent.EventId, decision.State, classification.Intent, classification.Sentiment, decision.Reason, DateTimeOffset.UtcNow));
        await commentRepository.UpdateAnalysisAsync(rawEvent.CommentId, classification.Intent, classification.Sentiment, decision.State, cancellationToken);
    }

    private static bool IsSelfGeneratedByPage(RawEvent rawEvent)
    {
        return rawEvent.EventType == "comment_created" &&
               !string.IsNullOrWhiteSpace(rawEvent.UserId) &&
               string.Equals(rawEvent.UserId, rawEvent.PageId, StringComparison.Ordinal);
    }
}
