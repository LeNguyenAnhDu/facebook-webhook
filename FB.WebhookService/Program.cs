using FB.Shared.Configuration;
using FB.Shared.Kafka;
using FB.WebhookService.Options;
using FB.WebhookService.Services;

DotEnvLoader.LoadFromRepositoryRoot();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.Configure<FacebookWebhookOptions>(builder.Configuration.GetSection(FacebookWebhookOptions.SectionName));
builder.Services.AddKafkaProducer(builder.Configuration);
builder.Services.AddScoped<IFacebookWebhookSignatureValidator, FacebookWebhookSignatureValidator>();
builder.Services.AddScoped<IFacebookWebhookEventNormalizer, FacebookWebhookEventNormalizer>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new
{
    service = "webhook-service",
    status = "healthy",
    timestamp = DateTimeOffset.UtcNow
}));

app.Run();
