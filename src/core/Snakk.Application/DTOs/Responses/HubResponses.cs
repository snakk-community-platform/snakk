namespace Snakk.Application.DTOs.Responses;

public record HubResponse(
    string PublicId,
    string CommunityId,
    string Name,
    string Slug,
    string? Description,
    DateTime CreatedAt,
    int SpaceCount,
    int DiscussionCount,
    int ReplyCount,
    DateTime? LastModifiedAt = null);
