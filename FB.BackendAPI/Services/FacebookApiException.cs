namespace FB.BackendAPI.Services;

public sealed class FacebookApiException : Exception
{
    public FacebookApiException(string errorCode, string message, int statusCode, bool isRetryable = true, string? details = null)
        : base(message)
    {
        ErrorCode = errorCode;
        StatusCode = statusCode;
        IsRetryable = isRetryable;
        Details = details;
    }

    public string ErrorCode { get; }

    public int StatusCode { get; }

    public bool IsRetryable { get; }

    public string? Details { get; }
}
