namespace FB.BackendAPI.Options;

public sealed class KafkaConsumerOptions
{
    public const string SectionName = "KafkaConsumer";

    public string ReplyCommandsGroupId { get; set; } = "backend-api-reply-commands";

    public string SendRetryGroupId { get; set; } = "backend-api-send-retry";
}
