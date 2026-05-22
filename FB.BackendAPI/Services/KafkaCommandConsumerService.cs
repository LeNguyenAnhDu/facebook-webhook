using System.Text.Json;
using Confluent.Kafka;
using FB.BackendAPI.Models;
using FB.BackendAPI.Options;
using FB.Shared.Constants;
using FB.Shared.Contracts;
using FB.Shared.Kafka;
using Microsoft.Extensions.Options;

namespace FB.BackendAPI.Services;

public sealed class KafkaCommandConsumerService : BackgroundService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly IServiceProvider _serviceProvider;
    private readonly KafkaOptions _kafkaOptions;
    private readonly KafkaConsumerOptions _consumerOptions;
    private readonly ILogger<KafkaCommandConsumerService> _logger;

    public KafkaCommandConsumerService(
        IServiceProvider serviceProvider,
        IOptions<KafkaOptions> kafkaOptions,
        IOptions<KafkaConsumerOptions> consumerOptions,
        ILogger<KafkaCommandConsumerService> logger)
    {
        _serviceProvider = serviceProvider;
        _kafkaOptions = kafkaOptions.Value;
        _consumerOptions = consumerOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.WhenAll(
            ConsumeReplyCommandsAsync(stoppingToken),
            ConsumeSendRetryAsync(stoppingToken));
    }

    private Task ConsumeReplyCommandsAsync(CancellationToken stoppingToken)
    {
        return Task.Run(() =>
        {
            var config = BuildConsumerConfig(_consumerOptions.ReplyCommandsGroupId);
            using var consumer = new ConsumerBuilder<Ignore, string>(config).Build();
            consumer.Subscribe(KafkaTopics.ReplyCommands);

            while (!stoppingToken.IsCancellationRequested)
            {
                ConsumeResult<Ignore, string>? result = null;
                try
                {
                    result = consumer.Consume(stoppingToken);
                    var command = JsonSerializer.Deserialize<ReplyCommand>(result.Message.Value, SerializerOptions);
                    if (command is null)
                    {
                        consumer.Commit(result);
                        continue;
                    }

                    using var scope = _serviceProvider.CreateScope();
                    var commandService = scope.ServiceProvider.GetRequiredService<IFacebookCommandService>();
                    var statusStore = scope.ServiceProvider.GetRequiredService<ICommandStatusStore>();
                    statusStore.Upsert(new CommandStatusSnapshot(command.CommandId, command.EventId, ProcessingState.Received, command.Action, "Received from reply_commands.", DateTimeOffset.UtcNow));

                    ProcessReplyCommandAsync(commandService, statusStore, command, stoppingToken).GetAwaiter().GetResult();
                    consumer.Commit(result);
                }
                catch (JsonException exception)
                {
                    _logger.LogError(exception, "Skipped malformed JSON message from reply_commands. Payload={Payload}", result?.Message.Value);
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
                    _logger.LogError(exception, "Failed while consuming reply_commands.");
                    if (result is not null)
                    {
                        Thread.Sleep(500);
                    }
                }
            }
        }, stoppingToken);
    }

    private Task ConsumeSendRetryAsync(CancellationToken stoppingToken)
    {
        return Task.Run(() =>
        {
            var config = BuildConsumerConfig(_consumerOptions.SendRetryGroupId);
            using var consumer = new ConsumerBuilder<Ignore, string>(config).Build();
            consumer.Subscribe(KafkaTopics.SendRetry);

            while (!stoppingToken.IsCancellationRequested)
            {
                ConsumeResult<Ignore, string>? result = null;
                try
                {
                    result = consumer.Consume(stoppingToken);
                    var retryEvent = JsonSerializer.Deserialize<SendFailedEvent>(result.Message.Value, SerializerOptions);
                    if (retryEvent is null)
                    {
                        consumer.Commit(result);
                        continue;
                    }

                    using var scope = _serviceProvider.CreateScope();
                    var commandService = scope.ServiceProvider.GetRequiredService<IFacebookCommandService>();
                    var statusStore = scope.ServiceProvider.GetRequiredService<ICommandStatusStore>();
                    statusStore.Upsert(new CommandStatusSnapshot(retryEvent.CommandId, retryEvent.EventId, ProcessingState.Received, retryEvent.Payload.Action, $"Retry attempt {retryEvent.RetryCount}.", DateTimeOffset.UtcNow));

                    ProcessRetryAsync(commandService, statusStore, retryEvent, stoppingToken).GetAwaiter().GetResult();
                    consumer.Commit(result);
                }
                catch (JsonException exception)
                {
                    _logger.LogError(exception, "Skipped malformed JSON message from send_retry. Payload={Payload}", result?.Message.Value);
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
                    _logger.LogError(exception, "Failed while consuming send_retry.");
                    if (result is not null)
                    {
                        Thread.Sleep(500);
                    }
                }
            }
        }, stoppingToken);
    }

    private ConsumerConfig BuildConsumerConfig(string groupId)
    {
        return new ConsumerConfig
        {
            BootstrapServers = _kafkaOptions.BootstrapServers,
            GroupId = groupId,
            EnableAutoCommit = false,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            AllowAutoCreateTopics = false
        };
    }

    private async Task ProcessReplyCommandAsync(IFacebookCommandService commandService, ICommandStatusStore statusStore, ReplyCommand command, CancellationToken cancellationToken)
    {
        statusStore.Upsert(new CommandStatusSnapshot(command.CommandId, command.EventId, ProcessingState.Processing, command.Action, command.Reason, DateTimeOffset.UtcNow));

        if (command.Action == "reply" && !string.IsNullOrWhiteSpace(command.Target.CommentId) && !string.IsNullOrWhiteSpace(command.ReplyText))
        {
            await commandService.ReplyToCommentAsync(
                command.Target.CommentId,
                new ReplyToCommentRequest(command.ReplyText, command.CommandId, command.EventId, 0),
                cancellationToken);

            statusStore.Upsert(new CommandStatusSnapshot(command.CommandId, command.EventId, ProcessingState.Replied, command.Action, "Reply sent to Facebook.", DateTimeOffset.UtcNow));
            return;
        }

        if (command.Action == "hide_comment" && !string.IsNullOrWhiteSpace(command.Target.CommentId))
        {
            await commandService.HideCommentAsync(
                command.Target.CommentId,
                new HideCommentRequest(true, command.CommandId, command.EventId, 0),
                cancellationToken);

            statusStore.Upsert(new CommandStatusSnapshot(command.CommandId, command.EventId, ProcessingState.Processed, command.Action, "Comment hidden on Facebook.", DateTimeOffset.UtcNow));
        }
    }

    private async Task ProcessRetryAsync(IFacebookCommandService commandService, ICommandStatusStore statusStore, SendFailedEvent retryEvent, CancellationToken cancellationToken)
    {
        statusStore.Upsert(new CommandStatusSnapshot(retryEvent.CommandId, retryEvent.EventId, ProcessingState.Processing, retryEvent.Payload.Action, $"Retry count {retryEvent.RetryCount}.", DateTimeOffset.UtcNow));

        if (retryEvent.Payload.Action == "reply" && !string.IsNullOrWhiteSpace(retryEvent.Payload.Target.CommentId) && !string.IsNullOrWhiteSpace(retryEvent.Payload.ReplyText))
        {
            await commandService.ReplyToCommentAsync(
                retryEvent.Payload.Target.CommentId,
                new ReplyToCommentRequest(retryEvent.Payload.ReplyText, retryEvent.CommandId, retryEvent.EventId, retryEvent.RetryCount),
                cancellationToken);

            statusStore.Upsert(new CommandStatusSnapshot(retryEvent.CommandId, retryEvent.EventId, ProcessingState.Replied, retryEvent.Payload.Action, "Reply succeeded after retry.", DateTimeOffset.UtcNow));
            return;
        }

        if (retryEvent.Payload.Action == "hide_comment" && !string.IsNullOrWhiteSpace(retryEvent.Payload.Target.CommentId))
        {
            await commandService.HideCommentAsync(
                retryEvent.Payload.Target.CommentId,
                new HideCommentRequest(true, retryEvent.CommandId, retryEvent.EventId, retryEvent.RetryCount),
                cancellationToken);

            statusStore.Upsert(new CommandStatusSnapshot(retryEvent.CommandId, retryEvent.EventId, ProcessingState.Processed, retryEvent.Payload.Action, "Hide succeeded after retry.", DateTimeOffset.UtcNow));
        }
    }
}
