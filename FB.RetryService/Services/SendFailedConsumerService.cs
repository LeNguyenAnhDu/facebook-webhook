using System.Text.Json;
using Confluent.Kafka;
using FB.RetryService.Options;
using FB.Shared.Constants;
using FB.Shared.Contracts;
using FB.Shared.Kafka;
using Microsoft.Extensions.Options;

namespace FB.RetryService.Services;

public sealed class SendFailedConsumerService : BackgroundService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly KafkaOptions _kafkaOptions;
    private readonly KafkaConsumerOptions _consumerOptions;
    private readonly RetryProcessingOptions _retryOptions;
    private readonly IKafkaProducer _kafkaProducer;
    private readonly ILogger<SendFailedConsumerService> _logger;

    public SendFailedConsumerService(
        IOptions<KafkaOptions> kafkaOptions,
        IOptions<KafkaConsumerOptions> consumerOptions,
        IOptions<RetryProcessingOptions> retryOptions,
        IKafkaProducer kafkaProducer,
        ILogger<SendFailedConsumerService> logger)
    {
        _kafkaOptions = kafkaOptions.Value;
        _consumerOptions = consumerOptions.Value;
        _retryOptions = retryOptions.Value;
        _kafkaProducer = kafkaProducer;
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
                AllowAutoCreateTopics = false
            };

            using var consumer = new ConsumerBuilder<Ignore, string>(config).Build();
            consumer.Subscribe(KafkaTopics.SendFailed);

            while (!stoppingToken.IsCancellationRequested)
            {
                ConsumeResult<Ignore, string>? result = null;
                try
                {
                    result = consumer.Consume(stoppingToken);
                    var failedEvent = JsonSerializer.Deserialize<SendFailedEvent>(result.Message.Value, SerializerOptions);
                    if (failedEvent is null)
                    {
                        consumer.Commit(result);
                        continue;
                    }

                    await ProcessAsync(failedEvent, stoppingToken);
                    consumer.Commit(result);
                }
                catch (JsonException exception)
                {
                    _logger.LogError(exception, "Skipped malformed JSON message from send_failed. Payload={Payload}", result?.Message.Value);
                    if (result is not null)
                    {
                        consumer.Commit(result);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Failed while consuming send_failed.");
                    await Task.Delay(500, stoppingToken);
                }
            }
        }, stoppingToken);
    }

    private async Task ProcessAsync(SendFailedEvent failedEvent, CancellationToken cancellationToken)
    {
        var attemptNumber = failedEvent.RetryCount + 1;
        var delaySeconds = _retryOptions.BaseDelaySeconds * (int)Math.Pow(2, failedEvent.RetryCount);

        if (!failedEvent.IsRetryable)
        {
            var deadLetter = new DeadLetterEvent
            {
                CommandId = failedEvent.CommandId,
                EventId = failedEvent.EventId,
                RetryCount = failedEvent.RetryCount,
                FailedAt = DateTimeOffset.UtcNow,
                FinalError = $"{failedEvent.ErrorCode}: {failedEvent.LastError}",
                OriginalTopic = KafkaTopics.SendFailed,
                Payload = failedEvent.Payload
            };

            await _kafkaProducer.ProduceAsync(KafkaTopics.DeadLetter, deadLetter, cancellationToken);
            _logger.LogWarning("Moved non-retryable command {CommandId} to dead_letter immediately. ErrorCode={ErrorCode}", failedEvent.CommandId, failedEvent.ErrorCode);
            return;
        }

        if (attemptNumber > _retryOptions.MaxRetries)
        {
            var deadLetter = new DeadLetterEvent
            {
                CommandId = failedEvent.CommandId,
                EventId = failedEvent.EventId,
                RetryCount = failedEvent.RetryCount,
                FailedAt = DateTimeOffset.UtcNow,
                FinalError = $"{failedEvent.ErrorCode}: {failedEvent.LastError}",
                OriginalTopic = KafkaTopics.SendFailed,
                Payload = failedEvent.Payload
            };

            await _kafkaProducer.ProduceAsync(KafkaTopics.DeadLetter, deadLetter, cancellationToken);
            _logger.LogWarning("Moved command {CommandId} to dead_letter after {RetryCount} retries.", failedEvent.CommandId, failedEvent.RetryCount);
            return;
        }

        var nextRetryAt = DateTimeOffset.UtcNow.AddSeconds(delaySeconds);
        await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);

        var retryEvent = failedEvent with
        {
            RetryCount = attemptNumber,
            NextRetryAt = nextRetryAt
        };

        await _kafkaProducer.ProduceAsync(KafkaTopics.SendRetry, retryEvent, cancellationToken);
        _logger.LogInformation("Scheduled retry {RetryCount} for command {CommandId} at {NextRetryAt}.", retryEvent.RetryCount, retryEvent.CommandId, retryEvent.NextRetryAt);
    }
}
