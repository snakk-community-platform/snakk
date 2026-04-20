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
        int pageSize = 20,
        string? userId = null);

    Task<PagedResult<PostSearchResultDto>> SearchPostsAsync(
        string query,
        string? authorPublicId = null,
        string? discussionPublicId = null,
        string? spacePublicId = null,
        int offset = 0,
        int pageSize = 20,
        string? userId = null);

    Task<int> GetDiscussionCountByAuthorAsync(string authorPublicId);
    Task<int> GetPostCountByAuthorAsync(string authorPublicId);
    Task<int> GetDiscussionPostCountAsync(string discussionPublicId);

    /// <summary>
    /// Gets discussions by space with enriched author and count data
    /// </summary>
    Task<PagedResult<DiscussionListItemDto>> GetDiscussionsBySpaceAsync(
        string spacePublicId,
        int offset = 0,
        int pageSize = 20,
        int? typeFilter = null,
        string? userId = null,
        string? cursor = null);

    /// <summary>
    /// Gets all hubs with their statistics
    /// </summary>
    Task<PagedResult<HubListItemDto>> GetHubsAsync(
        int offset = 0,
        int pageSize = 20,
        string? userId = null);

    /// <summary>
    /// Gets spaces by hub with their statistics
    /// </summary>
    Task<PagedResult<SpaceListItemDto>> GetSpacesByHubAsync(
        string hubPublicId,
        int offset = 0,
        int pageSize = 20,
        string? userId = null);

    /// <summary>
    /// Searches spaces by name, optionally scoped to a hub or community,
    /// sorted by discussion count descending.
    /// </summary>
    Task<List<SpaceSearchItemDto>> SearchSpacesAsync(
        string? query = null,
        string? hubPublicId = null,
        string? communityPublicId = null,
        int limit = 10,
        string? userId = null);

    /// <summary>
    /// Gets all discussions for sitemap generation
    /// </summary>
    Task<(List<SitemapDiscussionDto> Items, int TotalCount)> GetSitemapDiscussionsAsync(int page, int pageSize);

    /// <summary>
    /// Gets recent discussions with detailed information across all communities
    /// </summary>
    Task<PagedResult<RecentDiscussionDto>> GetRecentDiscussionsAsync(
        int offset,
        int pageSize,
        string? communityId = null,
        string? hubId = null,
        string? spaceId = null,
        string? cursor = null,
        string? userId = null,
        string? authorId = null);

    /// <summary>
    /// Gets trending discussions ordered by decay-weighted activity score (TrendScore DESC).
    /// </summary>
    Task<PagedResult<RecentDiscussionDto>> GetTrendingDiscussionsAsync(
        int offset,
        int pageSize,
        string? communityId = null,
        string? cursor = null,
        string? userId = null);

    /// <summary>
    /// Gets top discussions ordered by combined engagement (ReactionCount + PostCount DESC),
    /// optionally filtered to discussions created within a time window.
    /// </summary>
    Task<PagedResult<RecentDiscussionDto>> GetTopDiscussionsAsync(
        int offset,
        int pageSize,
        string? communityId = null,
        string? timePeriod = null,
        string? userId = null);

    /// <summary>
    /// Gets newest discussions ordered by CreatedAt DESC (ignores reply bumps).
    /// </summary>
    Task<PagedResult<RecentDiscussionDto>> GetNewDiscussionsAsync(
        int offset,
        int pageSize,
        string? communityId = null,
        string? cursor = null,
        string? userId = null);

    /// <summary>
    /// Fetches preview data for a set of discussions by their public IDs.
    /// Used to enrich saved/followed discussion lists with type-specific previews.
    /// </summary>
    Task<Dictionary<string, DiscussionPreviewDto>> FetchPreviewsByPublicIdsAsync(IEnumerable<string> publicIds);

    /// <summary>
    /// Fetches full RecentDiscussionDto (including previews) for a set of public IDs.
    /// </summary>
    Task<List<RecentDiscussionDto>> GetRecentDiscussionsByPublicIdsAsync(IEnumerable<string> publicIds);
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
    LatestDiscussionDto? LatestDiscussion,
    string? AvatarFileName = null);

public record LatestDiscussionDto(
    string PublicId,
    string Title,
    string Slug,
    DateTime LastActivityAt,
    string AuthorPublicId,
    string AuthorDisplayName,
    string? AuthorAvatarFileName,
    int PostCount,
    string? AuthorAvatarThumbnailFileName = null);

public record DiscussionListItemDto(
    string PublicId,
    string SpacePublicId,
    string Title,
    string Slug,
    int Type,
    DateTime CreatedAt,
    DateTime? LastActivityAt,
    bool IsPinned,
    bool IsLocked,
    int PostCount,
    int ReactionCount,
    string AuthorPublicId,
    string AuthorDisplayName,
    string? AuthorAvatarFileName,
    string? Tags,
    string? AuthorAvatarThumbnailFileName = null);

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
    string HubName,
    string CommunitySlug,
    DateTime CreatedAt,
    DateTime? LastActivityAt,
    int PostCount,
    int ReactionCount,
    int ViewCount,
    string? AuthorAvatarThumbnailFileName = null);

public record PostSearchResultDto(
    string PublicId,
    string RenderedContent,
    string AuthorPublicId,
    string AuthorDisplayName,
    string? AuthorAvatarFileName,
    string DiscussionPublicId,
    string DiscussionTitle,
    string DiscussionSlug,
    string SpaceSlug,
    string SpaceName,
    string HubSlug,
    string HubName,
    string CommunitySlug,
    DateTime CreatedAt,
    string? AuthorAvatarThumbnailFileName = null);

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
    int Type,
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
    string? CreatedByUserAvatarThumbnailFileName,
    int PostCount,
    int ReactionCount,
    string[] Tags,
    DiscussionPreviewDto? Preview = null);

// ── Discussion preview sub-DTOs ──
public record DiscussionPreviewDto(
    PollPreviewDto? Poll = null,
    DebatePreviewDto? Debate = null,
    LinkPreviewDto? Link = null,
    ImagesPreviewDto? Images = null,
    IamaPreviewDto? Iama = null);

public record PollPreviewDto(IReadOnlyList<PollOptionPreviewDto> Options, int TotalVotes, bool IsSecret, DateTime? ClosesAt);
public record PollOptionPreviewDto(string Text, int VoteCount);

public record DebatePreviewDto(IReadOnlyList<DebatePositionPreviewDto> Positions);
public record DebatePositionPreviewDto(string Label, int Index, int PostCount);

public record LinkPreviewDto(
    string Url, string? Title, string? Description, string? Domain,
    string? ImageUrl, string? ImagePath, string? ImageThumbnailPath, string? OEmbedHtml, bool IsInternal);

public record ImagesPreviewDto(int ImageCount, IReadOnlyList<ImagePreviewItemDto> Items, bool IsSpoiler, string Layout);
public record ImagePreviewItemDto(
    string Url,
    string? ThumbnailUrl,
    string? MediumThumbnailUrl,
    string? BlurDataUri,
    int Width,
    int Height,
    int? ThumbnailWidth,
    int? ThumbnailHeight,
    int? MediumThumbnailWidth,
    int? MediumThumbnailHeight);

public record IamaPreviewDto(
    int Phase,
    DateTime? ScheduledStartUtc,
    DateTime? ScheduledEndUtc,
    int OfficialAnswerCount,
    int BestQuestionCount,
    bool IsVerified);

public record SpaceSearchItemDto(
    string PublicId,
    string Name,
    string Slug,
    string HubSlug,
    string HubName,
    string CommunitySlug,
    int DiscussionCount,
    string CommunityName,
    string AvatarUrl);
