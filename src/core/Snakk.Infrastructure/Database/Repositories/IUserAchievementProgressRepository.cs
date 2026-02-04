namespace Snakk.Infrastructure.Database.Repositories;

using Snakk.Infrastructure.Database.Entities;

public interface IUserAchievementProgressRepository : IGenericDatabaseRepository<UserAchievementProgressDatabaseEntity>
{
    Task<UserAchievementProgressDatabaseEntity?> GetByUserAndAchievementAsync(int userId, int achievementId);
    Task<IEnumerable<UserAchievementProgressDatabaseEntity>> GetByUserIdAsync(int userId);
    Task<IEnumerable<UserAchievementProgressDatabaseEntity>> GetIncompleteByUserIdAsync(int userId);
}
