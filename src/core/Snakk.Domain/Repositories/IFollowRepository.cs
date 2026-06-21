namespace Snakk.Domain.Repositories;

using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;

public interface IFollowRepository
{
    Task<Follow?> GetByUserAndDiscussionAsync(UserId userId, DiscussionId discussionId, CancellationToken ct = default);
    Task<Follow?> GetByUserAndSpaceAsync(UserId userId, SpaceId spaceId, CancellationToken ct = default);
    Task<Follow?> GetByUserAndFollowedUserAsync(UserId userId, UserId followedUserId, CancellationToken ct = default);
    Task<IEnumerable<UserId>> GetFollowersOfDiscussionAsync(DiscussionId discussionId, CancellationToken ct = default);
    Task<IEnumerable<UserId>> GetFollowersOfSpaceAsync(SpaceId spaceId, CancellationToken ct = default);
    Task<IEnumerable<UserId>> GetFollowersOfUserAsync(UserId userId, CancellationToken ct = default);
    Task<int> GetFollowerCountOfUserAsync(UserId userId, CancellationToken ct = default);
    Task<int> GetFollowingCountByUserAsync(UserId userId, CancellationToken ct = default);
    Task<bool> IsFollowingDiscussionAsync(UserId userId, DiscussionId discussionId, CancellationToken ct = default);
    Task<bool> IsFollowingSpaceAsync(UserId userId, SpaceId spaceId, CancellationToken ct = default);
    Task<bool> IsFollowingUserAsync(UserId userId, UserId followedUserId, CancellationToken ct = default);
    Task<IEnumerable<(UserId UserId, FollowLevel Level)>> GetFollowersOfSpaceWithLevelAsync(SpaceId spaceId, CancellationToken ct = default);
    Task<IEnumerable<SpaceId>> GetFollowedSpacesByUserAsync(UserId userId, CancellationToken ct = default);
    Task<IEnumerable<DiscussionId>> GetFollowedDiscussionsByUserAsync(UserId userId, CancellationToken ct = default);
    Task<IEnumerable<(DiscussionId Id, DateTime FollowedAt)>> GetFollowedDiscussionsWithTimestampsAsync(UserId userId, CancellationToken ct = default);
    Task<IEnumerable<UserId>> GetFollowedUsersByUserAsync(UserId userId, CancellationToken ct = default);
    Task AddAsync(Follow follow, CancellationToken ct = default);
    Task UpdateAsync(Follow follow, CancellationToken ct = default);
    Task<bool> DeleteAsync(Follow follow, CancellationToken ct = default);
}
