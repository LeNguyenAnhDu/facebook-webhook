using FB.BackendAPI.Auth;
using FB.BackendAPI.Models;
using FB.BackendAPI.Services;
using FB.Shared.Api;
using Microsoft.AspNetCore.Mvc;

namespace FB.BackendAPI.Controllers;

[ApiController]
[Route("api/facebook")]
[ServiceFilter(typeof(AdminTokenAuthFilter))]
public sealed class FacebookAdminController : ControllerBase
{
    private readonly IFacebookGraphService _facebookGraphService;
    private readonly IFacebookCommandService _facebookCommandService;

    public FacebookAdminController(IFacebookGraphService facebookGraphService, IFacebookCommandService facebookCommandService)
    {
        _facebookGraphService = facebookGraphService;
        _facebookCommandService = facebookCommandService;
    }

    [HttpGet("posts")]
    [ProducesResponseType(typeof(ApiResponse<FacebookListResponse<FacebookPostSummary>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<FacebookListResponse<FacebookPostSummary>>>> GetPosts(
        [FromQuery] GetPostsRequest request,
        CancellationToken cancellationToken)
    {
        var posts = await _facebookGraphService.GetPostsAsync(request.Limit, cancellationToken);
        return Ok(ApiResponse<FacebookListResponse<FacebookPostSummary>>.Ok(posts));
    }

    [HttpPost("posts")]
    [ProducesResponseType(typeof(ApiResponse<FacebookMutationResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<FacebookMutationResponse>>> CreatePost(
        [FromBody] CreatePostRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _facebookGraphService.CreatePostAsync(request.Message, cancellationToken);
        return Ok(ApiResponse<FacebookMutationResponse>.Ok(response));
    }

    [HttpPost("comments/{commentId}/reply")]
    [ProducesResponseType(typeof(ApiResponse<FacebookMutationResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<FacebookMutationResponse>>> ReplyToComment(
        string commentId,
        [FromBody] ReplyToCommentRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _facebookCommandService.ReplyToCommentAsync(commentId, request, cancellationToken);
        return Ok(ApiResponse<FacebookMutationResponse>.Ok(response));
    }

    [HttpPost("comments/{commentId}/hide")]
    [ProducesResponseType(typeof(ApiResponse<FacebookMutationResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<FacebookMutationResponse>>> HideComment(
        string commentId,
        [FromBody] HideCommentRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _facebookCommandService.HideCommentAsync(commentId, request, cancellationToken);
        return Ok(ApiResponse<FacebookMutationResponse>.Ok(response));
    }
}
