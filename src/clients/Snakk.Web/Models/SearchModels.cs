namespace Snakk.Web.Models;

public record DiscussionSearchResultDto(
    string PublicId,
    string Title,
    string Slug,
    string AuthorPublicId,
    string AuthorDisplayName,
    string? AuthorAvatarFileName,
    string SpacePublicId,
    string SpaceName,
    string SpaceSlug,
    string HubSlug,
    DateTime CreatedAt,
    DateTime? LastActivityAt,
    int PostCount,
    int ReactionCount);

public record PostSearchResultDto(
    string PublicId,
    string Content,
    string AuthorPublicId,
    string AuthorDisplayName,
    string? AuthorAvatarFileName,
    string DiscussionPublicId,
    string DiscussionTitle,
    string DiscussionSlug,
    string SpaceSlug,
    string HubSlug,
    DateTime CreatedAt);

public record UserProfileDto(
    string PublicId,
    string DisplayName,
    string? AvatarFileName,
    DateTime JoinedAt,
    DateTime? LastSeenAt,
    int DiscussionCount,
    int PostCount);
