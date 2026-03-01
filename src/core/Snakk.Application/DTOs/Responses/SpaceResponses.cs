namespace Snakk.Application.DTOs.Responses;

public record SpaceResponse(
    string PublicId,
    string HubId,
    string Name,
    string Slug,
    string? Description,
    DateTime CreatedAt,
    int DiscussionCount,
    int ReplyCount,
    DateTime? LastModifiedAt = null,
    LatestDiscussionRef? LatestDiscussion = null);

public record SpaceByHubResponse(
    string PublicId,
    string HubPublicId,
    string Name,
    string Slug,
    string? Description,
    DateTime CreatedAt,
    int DiscussionCount,
    int ReplyCount,
    LatestDiscussionRef? LatestDiscussion = null);

public record LatestDiscussionRef(
    string PublicId,
    string Title,
    string Slug,
    DateTime LastActivityAt,
    string AuthorPublicId,
    string AuthorDisplayName,
    string? AuthorAvatarFileName,
    int PostCount);
