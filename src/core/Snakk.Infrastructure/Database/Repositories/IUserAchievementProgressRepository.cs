namespace Snakk.Infrastructure.Database.Repositories;

using Snakk.Infrastructure.Database.Entities;

public interface IUserAchievementProgressRepository : IGenericDatabaseRepository<UserAchievementProgressDatabaseEntity>
{
    Task<UserAchievementProgressDatabaseEntity?> GetByUserAndAchievementAsync(int userId, int achievementId, CancellationToken ct = default);
    Task<IEnumerable<UserAchievementProgressDatabaseEntity>> GetByUserIdAsync(int userId, CancellationToken ct = default);
    Task<IEnumerable<UserAchievementProgressDatabaseEntity>> GetIncompleteByUserIdAsync(int userId, CancellationToken ct = default);
}
