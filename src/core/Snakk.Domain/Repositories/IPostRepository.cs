namespace Snakk.Domain.Repositories;

using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Models;

public interface IPostRepository
{
    Task<Post?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<Post?> GetByPublicIdAsync(PostId publicId, CancellationToken ct = default);
    Task<IEnumerable<Post>> GetByPublicIdsAsync(IEnumerable<PostId> publicIds, CancellationToken ct = default);
    Task<IEnumerable<Post>> GetByDiscussionIdAsync(DiscussionId discussionId, CancellationToken ct = default);
    Task<PagedResult<Post>> GetPagedByDiscussionIdAsync(DiscussionId discussionId, int offset, int pageSize, CancellationToken ct = default);

    Task AddAsync(Post post, CancellationToken ct = default);
    Task UpdateAsync(Post post, CancellationToken ct = default);
    Task DeleteAsync(Post post, CancellationToken ct = default);
    Task AddRevisionAsync(PostRevision revision, CancellationToken ct = default);
    Task<IEnumerable<PostRevision>> GetRevisionsAsync(PostId postId, CancellationToken ct = default);

    /// <summary>
    /// Gets the sequential number of a post based on creation time
    /// </summary>
    Task<int> GetPostNumberInDiscussionAsync(DiscussionId discussionId, DateTime createdAt, CancellationToken ct = default);

    /// <summary>
    /// Gets the first post in a discussion (ordered by creation time)
    /// </summary>
    Task<Post?> GetFirstPostByDiscussionIdAsync(DiscussionId discussionId, CancellationToken ct = default);

    /// <summary>
    /// Gets top contributors by post count since a given date
    /// </summary>
    Task<List<(UserId UserId, int PostCount)>> GetTopContributorsSinceAsync(
        DateTime since,
        HubId? hubId,
        SpaceId? spaceId,
        CommunityId? communityId,
        int limit,
        CancellationToken ct = default);

    /// <summary>
    /// Gets contributors ordered by most recent post time (no time filter)
    /// </summary>
    Task<List<(UserId UserId, DateTime LastPostAt)>> GetLatestContributorsAsync(
        HubId? hubId,
        SpaceId? spaceId,
        CommunityId? communityId,
        int limit,
        CancellationToken ct = default);

    /// <summary>
    /// Gets post activity counts grouped by date for a specific user (excludes first posts)
    /// </summary>
    Task<IEnumerable<(DateTime Date, int Count)>> GetActivityByDateAsync(UserId userId, DateTime startDate, CancellationToken ct = default);

    /// <summary>
    /// Gets the spaces where a user has the most posts, ranked by post count.
    /// </summary>
    Task<List<TopSpaceForUser>> GetTopSpacesForUserAsync(UserId userId, int limit, CancellationToken ct = default);
}

/// <summary>
/// Lightweight projection of a space where a user is most active.
/// </summary>
public record TopSpaceForUser(
    string SpacePublicId,
    string SpaceSlug,
    string SpaceName,
    string? SpaceAvatarFileName,
    string HubSlug,
    string CommunitySlug,
    int PostCount);
