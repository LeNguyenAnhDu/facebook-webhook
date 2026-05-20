namespace FB.CoreService.Options;

public sealed class AutomationOptions
{
    public const string SectionName = "Automation";

    public int RateLimitPerMinute { get; set; } = 20;

    public int RepeatSpamThreshold24Hours { get; set; } = 3;
}
