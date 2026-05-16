using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FB.Shared.Kafka;

public sealed class KafkaProducer : IKafkaProducer, IDisposable
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly IProducer<Null, string> _producer;
    private readonly ILogger<KafkaProducer> _logger;

    public KafkaProducer(IOptions<KafkaOptions> options, ILogger<KafkaProducer> logger)
    {
        _logger = logger;

        var config = new ProducerConfig
        {
            BootstrapServers = options.Value.BootstrapServers,
            ClientId = options.Value.ClientId,
            Acks = Acks.All,
            EnableIdempotence = true
        };

        _producer = new ProducerBuilder<Null, string>(config).Build();
    }

    public async Task ProduceAsync<TMessage>(string topic, TMessage message, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Serialize(message, SerializerOptions);
        var result = await _producer.ProduceAsync(
            topic,
            new Message<Null, string> { Value = payload },
            cancellationToken);

        _logger.LogInformation(
            "Published Kafka message to topic {Topic} at offset {Offset}",
            result.Topic,
            result.Offset.Value);
    }

    public void Dispose()
    {
        _producer.Flush(TimeSpan.FromSeconds(5));
        _producer.Dispose();
    }
}
