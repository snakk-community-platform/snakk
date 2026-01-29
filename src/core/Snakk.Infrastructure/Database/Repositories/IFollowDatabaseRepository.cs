namespace Snakk.Infrastructure.Database.Repositories;

using Snakk.Infrastructure.Database.Entities;

public interface IFollowDatabaseRepository : IGenericDatabaseRepository<FollowDatabaseEntity>
{
    Task<FollowDatabaseEntity?> GetByUserAndDiscussionAsync(int userId, int discussionId);
    Task<FollowDatabaseEntity?> GetByUserAndSpaceAsync(int userId, int spaceId);
    Task<FollowDatabaseEntity?> GetByUserAndFollowedUserAsync(int userId, int followedUserId);
    Task<IEnumerable<int>> GetFollowerUserIdsOfDiscussionAsync(int discussionId);
    Task<IEnumerable<int>> GetFollowerUserIdsOfSpaceAsync(int spaceId);
    Task<IEnumerable<int>> GetFollowerUserIdsOfUserAsync(int userId);
    Task<int> GetFollowerCountOfUserAsync(int userId);
    Task<IEnumerable<(int UserId, string Level)>> GetFollowersOfSpaceWithLevelAsync(int spaceId);
    Task<bool> IsFollowingDiscussionAsync(int userId, int discussionId);
    Task<bool> IsFollowingSpaceAsync(int userId, int spaceId);
    Task<bool> IsFollowingUserAsync(int userId, int followedUserId);
    Task<IEnumerable<string>> GetFollowedSpacePublicIdsByUserAsync(int userId);
    Task<IEnumerable<string>> GetFollowedDiscussionPublicIdsByUserAsync(int userId);
    Task<IEnumerable<string>> GetFollowedUserPublicIdsByUserAsync(int userId);
}
