namespace Snakk.Infrastructure.Database.Repositories;

using Snakk.Infrastructure.Database.Entities;

public interface IFollowDatabaseRepository : IGenericDatabaseRepository<UserFollowDatabaseEntity>
{
    Task<UserFollowDatabaseEntity?> GetByUserAndDiscussionAsync(int userId, int discussionId, CancellationToken ct = default);
    Task<UserFollowDatabaseEntity?> GetByUserAndSpaceAsync(int userId, int spaceId, CancellationToken ct = default);
    Task<UserFollowDatabaseEntity?> GetByUserAndFollowedUserAsync(int userId, int followedUserId, CancellationToken ct = default);
    Task<IEnumerable<int>> GetFollowerUserIdsOfDiscussionAsync(int discussionId, CancellationToken ct = default);
    Task<IEnumerable<int>> GetFollowerUserIdsOfSpaceAsync(int spaceId, CancellationToken ct = default);
    Task<IEnumerable<int>> GetFollowerUserIdsOfUserAsync(int userId, CancellationToken ct = default);
    Task<int> GetFollowerCountOfUserAsync(int userId, CancellationToken ct = default);
    Task<IEnumerable<(int UserId, int LevelId)>> GetFollowersOfSpaceWithLevelAsync(int spaceId, CancellationToken ct = default);
    Task<bool> IsFollowingDiscussionAsync(int userId, int discussionId, CancellationToken ct = default);
    Task<bool> IsFollowingSpaceAsync(int userId, int spaceId, CancellationToken ct = default);
    Task<bool> IsFollowingUserAsync(int userId, int followedUserId, CancellationToken ct = default);
    Task<IEnumerable<string>> GetFollowedSpacePublicIdsByUserAsync(int userId, CancellationToken ct = default);
    Task<IEnumerable<string>> GetFollowedDiscussionPublicIdsByUserAsync(int userId, CancellationToken ct = default);
    Task<IEnumerable<string>> GetFollowedUserPublicIdsByUserAsync(int userId, CancellationToken ct = default);
}
