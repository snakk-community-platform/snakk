namespace Snakk.Api.Models;

public record CreateDiscussionRequest(
    string SpaceId,
    string Title,
    string Slug,
    string FirstPostContent);
