using FB.CoreService.Options;
using FB.CoreService.Services;
using FB.Shared.Configuration;
using FB.Shared.Database;
using FB.Shared.Kafka;

DotEnvLoader.LoadFromRepositoryRoot();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.Configure<AiClassificationOptions>(builder.Configuration.GetSection(AiClassificationOptions.SectionName));
builder.Services.Configure<KafkaConsumerOptions>(builder.Configuration.GetSection(KafkaConsumerOptions.SectionName));
builder.Services.Configure<AutomationOptions>(builder.Configuration.GetSection(AutomationOptions.SectionName));
builder.Services.AddKafkaProducer(builder.Configuration);
builder.Services.AddPostgresDatabase(builder.Configuration);
builder.Services.AddHttpClient<IAiClassifier, OpenAiCompatibleClassifier>();
builder.Services.AddSingleton<IAiCircuitBreaker, InMemoryAiCircuitBreaker>();
builder.Services.AddSingleton<IEventProcessingStatusStore, InMemoryEventProcessingStatusStore>();
builder.Services.AddSingleton<IUserActivityStore, InMemoryUserActivityStore>();
builder.Services.AddSingleton<ISpamDetector, SpamDetector>();
builder.Services.AddSingleton<IAutomationRuleEngine, AutomationRuleEngine>();
builder.Services.AddSingleton<IAiClassifierFallback, HeuristicClassifierFallback>();
builder.Services.AddScoped<ICommentRepository, PostgresCommentRepository>();
builder.Services.AddHostedService<RawEventConsumerService>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new
{
    service = "core-service",
    status = "healthy",
    timestamp = DateTimeOffset.UtcNow
}))
.WithName("GetHealth");

app.Run();
