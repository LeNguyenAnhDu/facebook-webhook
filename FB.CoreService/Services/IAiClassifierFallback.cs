using FB.CoreService.Models;
using FB.Shared.Contracts;

namespace FB.CoreService.Services;

public interface IAiClassifierFallback
{
    AiClassificationResult Classify(RawEvent rawEvent);
}
