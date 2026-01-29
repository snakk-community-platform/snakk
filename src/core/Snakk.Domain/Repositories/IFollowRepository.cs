namespace Snakk.Domain.Repositories;

using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;

public interface IFollowRepository
{
    Task<Follow?> GetByUserAndDiscussionAsync(UserId userId, DiscussionId discussionId);
    Task<Follow?> GetByUserAndSpaceAsync(UserId userId, SpaceId spaceId);
    Task<Follow?> GetByUserAndFollowedUserAsync(UserId userId, UserId followedUserId);
    Task<IEnumerable<UserId>> GetFollowersOfDiscussionAsync(DiscussionId discussionId);
    Task<IEnumerable<UserId>> GetFollowersOfSpaceAsync(SpaceId spaceId);
    Task<IEnumerable<UserId>> GetFollowersOfUserAsync(UserId userId);
    Task<int> GetFollowerCountOfUserAsync(UserId userId);
    Task<bool> IsFollowingDiscussionAsync(UserId userId, DiscussionId discussionId);
    Task<bool> IsFollowingSpaceAsync(UserId userId, SpaceId spaceId);
    Task<bool> IsFollowingUserAsync(UserId userId, UserId followedUserId);
    Task<IEnumerable<(UserId UserId, FollowLevel Level)>> GetFollowersOfSpaceWithLevelAsync(SpaceId spaceId);
    Task<IEnumerable<SpaceId>> GetFollowedSpacesByUserAsync(UserId userId);
    Task<IEnumerable<DiscussionId>> GetFollowedDiscussionsByUserAsync(UserId userId);
    Task<IEnumerable<UserId>> GetFollowedUsersByUserAsync(UserId userId);
    Task AddAsync(Follow follow);
    Task UpdateAsync(Follow follow);
    Task DeleteAsync(Follow follow);
}
