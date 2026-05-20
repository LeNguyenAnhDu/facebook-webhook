namespace FB.CoreService.Options;

public sealed class KafkaConsumerOptions
{
    public const string SectionName = "KafkaConsumer";

    public string GroupId { get; set; } = "core-service-raw-events";
}
