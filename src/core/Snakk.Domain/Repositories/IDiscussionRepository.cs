namespace Snakk.Domain.Repositories;

using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Models;

public interface IDiscussionRepository
{
    Task<Discussion?> GetByIdAsync(int id);
    Task<Discussion?> GetByPublicIdAsync(DiscussionId publicId);
    Task<Discussion?> GetBySlugAsync(string slug);
    Task<IEnumerable<Discussion>> GetBySpaceIdAsync(SpaceId spaceId);
    Task<PagedResult<Discussion>> GetBySpaceIdAsync(SpaceId spaceId, int offset, int pageSize);
    Task<PagedResult<Discussion>> GetPagedBySpaceIdAsync(SpaceId spaceId, int offset, int pageSize);
    Task<IEnumerable<Discussion>> GetRecentAsync(int count = 10);
    Task AddAsync(Discussion discussion);
    Task UpdateAsync(Discussion discussion);

    /// <summary>
    /// Gets top active discussions by post count since a given date
    /// </summary>
    Task<List<TopActiveDiscussion>> GetTopActiveDiscussionsSinceAsync(
        DateTime since,
        HubId? hubId,
        SpaceId? spaceId,
        CommunityId? communityId,
        int limit,
        string? userId = null);

    /// <summary>
    /// Fetches lightweight summaries for multiple discussions in a single query.
    /// </summary>
    Task<IReadOnlyList<DiscussionSummary>> GetSummariesByPublicIdsAsync(IEnumerable<DiscussionId> publicIds);

    /// <summary>
    /// Gets discussion activity counts grouped by date for a specific user
    /// </summary>
    Task<IEnumerable<(DateTime Date, int Count)>> GetActivityByDateAsync(UserId userId, DateTime startDate);

    /// <summary>
    /// Gets a user's top discussions ranked by engagement (post count + reaction count).
    /// </summary>
    Task<List<TopDiscussionByUser>> GetTopDiscussionsByUserAsync(UserId userId, int limit);
}

/// <summary>
/// Lightweight projection of a top-ranked discussion by a user.
/// </summary>
public record TopDiscussionByUser(
    string PublicId,
    string Title,
    string Slug,
    int PostCount,
    int ReactionCount,
    DateTime CreatedAt,
    string SpaceSlug,
    string SpaceName,
    string HubSlug,
    string HubName,
    string CommunitySlug,
    string CommunityName);

/// <summary>
/// Lightweight projection used for batch fetches (e.g. following list).
/// </summary>
public record DiscussionSummary(
    string PublicId,
    string Title,
    string Slug,
    string SpacePublicId,
    DateTime CreatedAt,
    DateTime? LastActivityAt,
    bool IsPinned,
    bool IsLocked,
    int Type,
    int PostCount);

/// <summary>
/// DTO for top active discussions statistics
/// </summary>
public record TopActiveDiscussion(
    DiscussionId PublicId,
    string Title,
    string Slug,
    int PostCountToday,
    string SpacePublicId,
    string SpaceSlug,
    string SpaceName,
    string HubPublicId,
    string HubSlug,
    string HubName,
    string AuthorPublicId,
    string AuthorDisplayName,
    string CommunitySlug,
    string? AuthorAvatarFileName = null);
