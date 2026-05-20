using FB.Shared.Contracts;

namespace FB.CoreService.Services;

public sealed class SpamDetector : ISpamDetector
{
    public SpamDetectionResult Detect(RawEvent rawEvent, UserActivitySnapshot activity)
    {
        var message = TextNormalization.Normalize(rawEvent.Message?.Trim());
        var hasLink = message.Contains("http://") || message.Contains("https://") || message.Contains(".com");
        var repeatedChars = message.Length > 12 && message.GroupBy(c => c).Any(group => group.Key != ' ' && group.Count() >= 8);
        var repeatedMessage = activity.SameMessageCount24Hours >= 3;
        var obviousAd = message.Contains("ib ngay") || message.Contains("uu dai") || message.Contains("khuyen mai") || message.Contains("telegram");

        if ((hasLink && repeatedMessage) || (obviousAd && hasLink))
        {
            return new SpamDetectionResult(true, true, "Repeated link spam in 24h.");
        }

        if (hasLink || repeatedChars || repeatedMessage || obviousAd)
        {
            return new SpamDetectionResult(true, false, "Spam-like content detected.");
        }

        return new SpamDetectionResult(false, false, "No spam indicators detected.");
    }
}
