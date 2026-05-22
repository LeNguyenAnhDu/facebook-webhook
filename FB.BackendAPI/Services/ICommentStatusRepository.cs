namespace FB.BackendAPI.Services;

public interface ICommentStatusRepository
{
    Task UpdateStatusAsync(string? commentId, string status, CancellationToken cancellationToken = default);
}
