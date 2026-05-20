using FB.CoreService.Models;
using FB.Shared.Contracts;

namespace FB.CoreService.Services;

public interface IAiClassifier
{
    Task<AiClassificationResult> ClassifyAsync(RawEvent rawEvent, CancellationToken cancellationToken);
}
