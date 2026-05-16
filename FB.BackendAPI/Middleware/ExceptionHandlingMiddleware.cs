using System.Text.Json;
using FB.BackendAPI.Services;
using FB.Shared.Api;

namespace FB.BackendAPI.Middleware;

public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (FacebookApiException exception)
        {
            _logger.LogError(exception, "Facebook API error while handling {Method} {Path}", context.Request.Method, context.Request.Path);

            context.Response.StatusCode = exception.StatusCode;
            context.Response.ContentType = "application/json";

            var response = ApiResponse<object>.Fail(exception.ErrorCode, exception.Message, exception.Details);
            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Unhandled error while handling {Method} {Path}", context.Request.Method, context.Request.Path);

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";

            var response = ApiResponse<object>.Fail("internal_server_error", "Unexpected server error.", exception.Message);
            await context.Response.WriteAsync(JsonSerializer.Serialize(response));
        }
    }
}
