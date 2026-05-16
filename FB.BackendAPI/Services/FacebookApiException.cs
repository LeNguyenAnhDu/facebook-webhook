namespace FB.BackendAPI.Services;

public sealed class FacebookApiException : Exception
{
    public FacebookApiException(string errorCode, string message, int statusCode, string? details = null)
        : base(message)
    {
        ErrorCode = errorCode;
        StatusCode = statusCode;
        Details = details;
    }

    public string ErrorCode { get; }

    public int StatusCode { get; }

    public string? Details { get; }
}
