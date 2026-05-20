namespace FB.RetryService.Options;

public sealed class KafkaConsumerOptions
{
    public const string SectionName = "KafkaConsumer";

    public string GroupId { get; set; } = "retry-service-send-failed";
}
