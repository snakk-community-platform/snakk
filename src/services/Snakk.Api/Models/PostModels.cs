namespace Snakk.Api.Models;

public record CreatePostRequest(
    string DiscussionId,
    string Content,
    string? ReplyToPostId);

public record UpdatePostRequest(string Content);
