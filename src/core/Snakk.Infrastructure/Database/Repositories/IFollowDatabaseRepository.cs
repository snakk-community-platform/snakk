namespace Snakk.Infrastructure.Database.Repositories;

using Snakk.Infrastructure.Database.Entities;

public interface IFollowDatabaseRepository : IGenericDatabaseRepository<UserFollowDatabaseEntity>
{
    Task<UserFollowDatabaseEntity?> GetByUserAndDiscussionAsync(int userId, int discussionId);
    Task<UserFollowDatabaseEntity?> GetByUserAndSpaceAsync(int userId, int spaceId);
    Task<UserFollowDatabaseEntity?> GetByUserAndFollowedUserAsync(int userId, int followedUserId);
    Task<IEnumerable<int>> GetFollowerUserIdsOfDiscussionAsync(int discussionId);
    Task<IEnumerable<int>> GetFollowerUserIdsOfSpaceAsync(int spaceId);
    Task<IEnumerable<int>> GetFollowerUserIdsOfUserAsync(int userId);
    Task<int> GetFollowerCountOfUserAsync(int userId);
    Task<IEnumerable<(int UserId, int LevelId)>> GetFollowersOfSpaceWithLevelAsync(int spaceId);
    Task<bool> IsFollowingDiscussionAsync(int userId, int discussionId);
    Task<bool> IsFollowingSpaceAsync(int userId, int spaceId);
    Task<bool> IsFollowingUserAsync(int userId, int followedUserId);
    Task<IEnumerable<string>> GetFollowedSpacePublicIdsByUserAsync(int userId);
    Task<IEnumerable<string>> GetFollowedDiscussionPublicIdsByUserAsync(int userId);
    Task<IEnumerable<string>> GetFollowedUserPublicIdsByUserAsync(int userId);
}
