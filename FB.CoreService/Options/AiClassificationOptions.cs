namespace FB.CoreService.Options;

public sealed class AiClassificationOptions
{
    public const string SectionName = "AiClassification";

    public string Provider { get; set; } = "fallback";

    public string Endpoint { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 15;

    public int FailureThreshold { get; set; } = 5;

    public int BreakDurationSeconds { get; set; } = 30;
}
