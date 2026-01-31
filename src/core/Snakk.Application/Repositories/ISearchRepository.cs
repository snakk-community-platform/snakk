namespace Snakk.Application.Repositories;

using Snakk.Shared.Models;

public interface ISearchRepository
{
    Task<PagedResult<DiscussionSearchResultDto>> SearchDiscussionsAsync(
        string query,
        string? authorPublicId = null,
        string? spacePublicId = null,
        string? hubPublicId = null,
        int offset = 0,
        int pageSize = 20);

    Task<PagedResult<PostSearchResultDto>> SearchPostsAsync(
        string query,
        string? authorPublicId = null,
        string? discussionPublicId = null,
        string? spacePublicId = null,
        int offset = 0,
        int pageSize = 20);

    Task<int> GetDiscussionCountByAuthorAsync(string authorPublicId);
    Task<int> GetPostCountByAuthorAsync(string authorPublicId);

    /// <summary>
    /// Gets discussions by space with enriched author and count data
    /// </summary>
    Task<PagedResult<DiscussionListItemDto>> GetDiscussionsBySpaceAsync(
        string spacePublicId,
        int offset = 0,
        int pageSize = 20);

    /// <summary>
    /// Gets all hubs with their statistics
    /// </summary>
    Task<PagedResult<HubListItemDto>> GetHubsAsync(
        int offset = 0,
        int pageSize = 20);

    /// <summary>
    /// Gets spaces by hub with their statistics
    /// </summary>
    Task<PagedResult<SpaceListItemDto>> GetSpacesByHubAsync(
        string hubPublicId,
        int offset = 0,
        int pageSize = 20);

    /// <summary>
    /// Gets all discussions for sitemap generation
    /// </summary>
    Task<List<SitemapDiscussionDto>> GetSitemapDiscussionsAsync();

    /// <summary>
    /// Gets recent discussions with detailed information across all communities
    /// </summary>
    Task<PagedResult<RecentDiscussionDto>> GetRecentDiscussionsAsync(
        int offset,
        int pageSize,
        string? communityId = null,
        string? cursor = null);
}

public record HubListItemDto(
    string PublicId,
    string CommunityPublicId,
    string Name,
    string Slug,
    string? Description,
    DateTime CreatedAt,
    int SpaceCount,
    int DiscussionCount,
    int ReplyCount);

public record SpaceListItemDto(
    string PublicId,
    string HubPublicId,
    string Name,
    string Slug,
    string? Description,
    DateTime CreatedAt,
    int DiscussionCount,
    int ReplyCount,
    LatestDiscussionDto? LatestDiscussion);

public record LatestDiscussionDto(
    string PublicId,
    string Title,
    string Slug,
    DateTime LastActivityAt,
    string AuthorPublicId,
    string AuthorDisplayName,
    string? AuthorAvatarFileName,
    int PostCount);

public record DiscussionListItemDto(
    string PublicId,
    string SpacePublicId,
    string Title,
    string Slug,
    DateTime CreatedAt,
    DateTime? LastActivityAt,
    bool IsPinned,
    bool IsLocked,
    int PostCount,
    int ReactionCount,
    string AuthorPublicId,
    string AuthorDisplayName,
    string? AuthorAvatarFileName,
    string? Tags);

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
    int ViewCount);

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

public record SitemapDiscussionDto(
    string PublicId,
    string Slug,
    string HubSlug,
    string SpaceSlug,
    string CommunitySlug,
    DateTime LastModified,
    bool IsPinned);

public record RecentDiscussionDto(
    string PublicId,
    string Title,
    string Slug,
    DateTime CreatedAt,
    DateTime? LastActivityAt,
    bool IsPinned,
    bool IsLocked,
    string SpacePublicId,
    string SpaceSlug,
    string SpaceName,
    string HubPublicId,
    string HubSlug,
    string HubName,
    string CommunityPublicId,
    string CommunitySlug,
    string CommunityName,
    string CreatedByUserPublicId,
    string CreatedByUserDisplayName,
    string? CreatedByUserAvatarFileName,
    int PostCount,
    int ReactionCount,
    string[] Tags);
