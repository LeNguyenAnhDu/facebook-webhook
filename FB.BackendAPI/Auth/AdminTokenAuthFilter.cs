using FB.BackendAPI.Options;
using FB.Shared.Api;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace FB.BackendAPI.Auth;

public sealed class AdminTokenAuthFilter : IAsyncAuthorizationFilter
{
    private readonly DashboardAuthOptions _options;

    public AdminTokenAuthFilter(IOptions<DashboardAuthOptions> options)
    {
        _options = options.Value;
    }

    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var providedToken = context.HttpContext.Request.Headers[_options.HeaderName].FirstOrDefault();
        if (!string.Equals(providedToken, _options.AdminToken, StringComparison.Ordinal))
        {
            context.Result = new UnauthorizedObjectResult(
                ApiResponse<object>.Fail(
                    "unauthorized",
                    "Admin token is missing or invalid.",
                    $"Provide header '{_options.HeaderName}' with a valid admin token."));
        }

        return Task.CompletedTask;
    }
}
