namespace FB.RetryService.Options;

public sealed class RetryProcessingOptions
{
    public const string SectionName = "RetryProcessing";

    public int MaxRetries { get; set; } = 3;

    public int BaseDelaySeconds { get; set; } = 1;
}
