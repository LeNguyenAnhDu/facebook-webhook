using FB.BackendAPI.Models;

namespace FB.BackendAPI.Services;

public interface IFacebookGraphService
{
    Task<FacebookListResponse<FacebookPostSummary>> GetPostsAsync(int limit, CancellationToken cancellationToken);

    Task<FacebookMutationResponse> CreatePostAsync(string message, CancellationToken cancellationToken);

    Task<FacebookMutationResponse> ReplyToCommentAsync(string commentId, string message, CancellationToken cancellationToken);

    Task<FacebookMutationResponse> SetCommentHiddenAsync(string commentId, bool isHidden, CancellationToken cancellationToken);
}
