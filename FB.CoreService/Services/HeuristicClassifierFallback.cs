using FB.CoreService.Models;
using FB.Shared.Contracts;

namespace FB.CoreService.Services;

public sealed class HeuristicClassifierFallback : IAiClassifierFallback
{
    public AiClassificationResult Classify(RawEvent rawEvent)
    {
        var message = TextNormalization.Normalize(rawEvent.Message);
        var hasNegativeSignal =
            message.Contains("qua te") ||
            message.Contains("te") ||
            message.Contains("that vong") ||
            message.Contains("khong hai long") ||
            message.Contains("cho lau") ||
            message.Contains("chua nhan") ||
            message.Contains("loi") ||
            message.Contains("kem") ||
            message.Contains("khong tot") ||
            message.Contains("khong on");

        var hasPositiveSignal =
            message.Contains("rat tot") ||
            message.Contains("tot") ||
            message.Contains("cam on") ||
            message.Contains("ung ho") ||
            message.Contains("quay lai") ||
            message.Contains("hay qua") ||
            message.Contains("chuyen nghiep") ||
            message.Contains("tuyet voi");

        var intent = message switch
        {
            var text when text.Contains("gia") || text.Contains("bao nhieu") || text.Contains("bao gia") => "ask_price",
            var text when text.Contains("chua nhan") || text.Contains("khieu nai") || text.Contains("don hang") || text.Contains("cho lau") || text.Contains("loi") || text.Contains("khong hai long") => "support_request",
            var text when text.Contains("hay qua") || text.Contains("cam on") || text.Contains("tuyet") || text.Contains("rat tot") || text.Contains("chuyen nghiep") => "praise",
            _ => "general_engagement"
        };

        var sentiment = hasNegativeSignal
            ? "negative"
            : hasPositiveSignal
                ? "positive"
                : "neutral";

        return new AiClassificationResult(intent, sentiment, $"Heuristic fallback classification. intent={intent}, sentiment={sentiment}.", true);
    }
}
