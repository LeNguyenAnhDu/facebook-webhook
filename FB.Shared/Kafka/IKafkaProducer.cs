namespace FB.Shared.Kafka;

public interface IKafkaProducer
{
    Task ProduceAsync<TMessage>(string topic, TMessage message, CancellationToken cancellationToken = default);
}
