using FB.BackendAPI.Models;

namespace FB.BackendAPI.Services;

public interface IFacebookCommandService
{
    Task<FacebookMutationResponse> ReplyToCommentAsync(string commentId, ReplyToCommentRequest request, CancellationToken cancellationToken);

    Task<FacebookMutationResponse> HideCommentAsync(string commentId, HideCommentRequest request, CancellationToken cancellationToken);
}
