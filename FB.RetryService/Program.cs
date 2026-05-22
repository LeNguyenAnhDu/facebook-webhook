using FB.RetryService.Options;
using FB.RetryService.Services;
using FB.Shared.Configuration;
using FB.Shared.Kafka;

DotEnvLoader.LoadFromRepositoryRoot();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.Configure<RetryProcessingOptions>(builder.Configuration.GetSection(RetryProcessingOptions.SectionName));
builder.Services.Configure<KafkaConsumerOptions>(builder.Configuration.GetSection(KafkaConsumerOptions.SectionName));
builder.Services.AddKafkaProducer(builder.Configuration);
builder.Services.AddHostedService<SendFailedConsumerService>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/health", () => Results.Ok(new
{
    service = "retry-service",
    status = "healthy",
    timestamp = DateTimeOffset.UtcNow
}))
.WithName("GetHealth");

app.Run();
