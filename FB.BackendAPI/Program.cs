using System.Net;
using System.Text.Json;
using FB.BackendAPI.Auth;
using FB.BackendAPI.Middleware;
using FB.BackendAPI.Options;
using FB.BackendAPI.Services;
using FB.Shared.Api;
using FB.Shared.Contracts;
using FB.Shared.Configuration;
using FB.Shared.Database;
using FB.Shared.Kafka;
using Microsoft.OpenApi.Models;

DotEnvLoader.LoadFromRepositoryRoot();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("AdminToken", new OpenApiSecurityScheme
    {
        Description = "Nhap admin token vao day (FB_ADMIN_TOKEN_2026)",
        Name = "X-Admin-Token",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "AdminToken"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.Configure<FacebookGraphOptions>(builder.Configuration.GetSection(FacebookGraphOptions.SectionName));
builder.Services.Configure<DashboardAuthOptions>(builder.Configuration.GetSection(DashboardAuthOptions.SectionName));
builder.Services.Configure<KafkaConsumerOptions>(builder.Configuration.GetSection(KafkaConsumerOptions.SectionName));
builder.Services.Configure<CircuitBreakerOptions>(builder.Configuration.GetSection(CircuitBreakerOptions.SectionName));

builder.Services.AddKafkaProducer(builder.Configuration);
builder.Services.AddPostgresDatabase(builder.Configuration);
builder.Services.AddScoped<ICommandIdempotencyStore, PostgresCommandIdempotencyStore>();
builder.Services.AddScoped<ICommentStatusRepository, PostgresCommentStatusRepository>();
builder.Services.AddSingleton<ICommandStatusStore, InMemoryCommandStatusStore>();
builder.Services.AddSingleton<IFacebookCircuitBreaker, InMemoryFacebookCircuitBreaker>();
builder.Services.AddSingleton<AdminTokenAuthFilter>();
builder.Services.AddScoped<IFacebookCommandService, FacebookCommandService>();
builder.Services.AddHostedService<KafkaCommandConsumerService>();

builder.Services.AddHttpClient<IFacebookGraphService, FacebookGraphService>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IConfiguration>()
        .GetSection(FacebookGraphOptions.SectionName)
        .Get<FacebookGraphOptions>() ?? new FacebookGraphOptions();

    client.BaseAddress = new Uri($"https://graph.facebook.com/{options.GraphVersion}/");
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
}).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    AutomaticDecompression = DecompressionMethods.All
});

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();

app.MapGet("/health", () => Results.Ok(new
{
    service = "backend-api",
    status = "healthy",
    timestamp = DateTimeOffset.UtcNow
}));

app.MapGet("/api/command-status/{commandId}", (string commandId, ICommandStatusStore store) =>
{
    return store.TryGet(commandId, out var snapshot)
        ? Results.Ok(snapshot)
        : Results.NotFound(ApiResponse<object>.Fail("command_not_found", $"Command '{commandId}' was not found."));
});

app.Run();
