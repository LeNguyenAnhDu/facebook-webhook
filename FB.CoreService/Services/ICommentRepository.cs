using FB.Shared.Contracts;

namespace FB.CoreService.Services;

public interface ICommentRepository
{
    Task UpsertReceivedAsync(RawEvent rawEvent, CancellationToken cancellationToken = default);

    Task UpdateAnalysisAsync(string? commentId, string? intent, string? sentiment, string status, CancellationToken cancellationToken = default);
}
