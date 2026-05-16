namespace FB.Shared.Api;

public sealed record ApiError(string Code, string Message, string? Details = null);
