using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FB.Shared.Kafka;

public static class KafkaServiceCollectionExtensions
{
    public static IServiceCollection AddKafkaProducer(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<KafkaOptions>(configuration.GetSection(KafkaOptions.SectionName));
        services.AddSingleton<IKafkaProducer, KafkaProducer>();
        return services;
    }
}
