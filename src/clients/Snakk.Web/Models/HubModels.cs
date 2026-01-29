namespace Snakk.Web.Models;

public record HubDto(
    string PublicId,
    string CommunityId,
    string Name,
    string Slug,
    string? Description,
    DateTime CreatedAt,
    int SpaceCount = 0,
    int DiscussionCount = 0,
    int ReplyCount = 0);

public record HubDetailDto(
    string PublicId,
    string CommunityId,
    string Name,
    string Slug,
    string? Description,
    DateTime CreatedAt,
    DateTime? LastModifiedAt,
    int SpaceCount = 0,
    int DiscussionCount = 0,
    int ReplyCount = 0);
