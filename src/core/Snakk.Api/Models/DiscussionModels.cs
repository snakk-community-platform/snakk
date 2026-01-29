namespace Snakk.Api.Models;

public record CreateDiscussionRequest(
    string SpaceId,
    string UserId,
    string Title,
    string Slug,
    string FirstPostContent);
