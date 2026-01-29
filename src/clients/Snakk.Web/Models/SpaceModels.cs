namespace Snakk.Web.Models;

public record SpaceDto(
    string PublicId,
    string HubId,
    string Name,
    string Slug,
    string? Description,
    DateTime CreatedAt,
    int DiscussionCount = 0,
    int ReplyCount = 0,
    LatestDiscussionDto? LatestDiscussion = null);

public record LatestDiscussionDto(
    string PublicId,
    string Title,
    string Slug,
    DateTime LastActivityAt,
    string AuthorPublicId,
    string AuthorDisplayName,
    string? AuthorAvatarFileName,
    int PostCount);

public record SpaceDetailDto(
    string PublicId,
    string HubId,
    string Name,
    string Slug,
    string? Description,
    DateTime CreatedAt,
    DateTime? LastModifiedAt,
    int DiscussionCount = 0,
    int ReplyCount = 0);
