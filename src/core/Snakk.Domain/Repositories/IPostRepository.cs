namespace Snakk.Domain.Repositories;

using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Models;

public interface IPostRepository
{
    Task<Post?> GetByIdAsync(int id);
    Task<Post?> GetByPublicIdAsync(PostId publicId);
    Task<IEnumerable<Post>> GetByPublicIdsAsync(IEnumerable<PostId> publicIds);
    Task<IEnumerable<Post>> GetByDiscussionIdAsync(DiscussionId discussionId);
    Task<PagedResult<Post>> GetPagedByDiscussionIdAsync(DiscussionId discussionId, int offset, int pageSize);

    Task AddAsync(Post post);
    Task UpdateAsync(Post post);
    Task DeleteAsync(Post post);
    Task AddRevisionAsync(PostRevision revision);
    Task<IEnumerable<PostRevision>> GetRevisionsAsync(PostId postId);

    /// <summary>
    /// Gets the sequential number of a post based on creation time
    /// </summary>
    Task<int> GetPostNumberInDiscussionAsync(DiscussionId discussionId, DateTime createdAt);

    /// <summary>
    /// Gets the first post in a discussion (ordered by creation time)
    /// </summary>
    Task<Post?> GetFirstPostByDiscussionIdAsync(DiscussionId discussionId);

    /// <summary>
    /// Gets top contributors by post count since a given date
    /// </summary>
    Task<List<(UserId UserId, int PostCount)>> GetTopContributorsSinceAsync(
        DateTime since,
        HubId? hubId,
        SpaceId? spaceId,
        CommunityId? communityId,
        int limit);

    /// <summary>
    /// Gets contributors ordered by most recent post time (no time filter)
    /// </summary>
    Task<List<(UserId UserId, DateTime LastPostAt)>> GetLatestContributorsAsync(
        HubId? hubId,
        SpaceId? spaceId,
        CommunityId? communityId,
        int limit);

    /// <summary>
    /// Gets post activity counts grouped by date for a specific user (excludes first posts)
    /// </summary>
    Task<IEnumerable<(DateTime Date, int Count)>> GetActivityByDateAsync(UserId userId, DateTime startDate);

    /// <summary>
    /// Gets the spaces where a user has the most posts, ranked by post count.
    /// </summary>
    Task<List<TopSpaceForUser>> GetTopSpacesForUserAsync(UserId userId, int limit);
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
