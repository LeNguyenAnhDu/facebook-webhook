using FB.Shared.Contracts;

namespace FB.CoreService.Services;

public interface ISpamDetector
{
    SpamDetectionResult Detect(RawEvent rawEvent, UserActivitySnapshot activity);
}

public sealed record SpamDetectionResult(bool IsSpam, bool IsMalicious, string Reason);
