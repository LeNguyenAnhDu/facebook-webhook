namespace FB.CoreService.Models;

public sealed record AiClassificationResult(
    string Intent,
    string Sentiment,
    string Summary,
    bool UsedFallback);
