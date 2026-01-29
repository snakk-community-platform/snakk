namespace Snakk.Api.Models;

public record CreatePostRequest(
    string DiscussionId,
    string UserId,
    string Content,
    string? ReplyToPostId);
