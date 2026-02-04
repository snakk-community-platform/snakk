namespace Snakk.Infrastructure.Database.Repositories;

using Snakk.Infrastructure.Database.Entities;

public interface IUserAchievementRepository : IGenericDatabaseRepository<UserAchievementDatabaseEntity>
{
    Task<UserAchievementDatabaseEntity?> GetByPublicIdAsync(string publicId);
    Task<IEnumerable<UserAchievementDatabaseEntity>> GetByUserIdAsync(int userId);
    Task<IEnumerable<UserAchievementDatabaseEntity>> GetDisplayedByUserIdAsync(int userId);
    Task<bool> HasAchievementAsync(int userId, int achievementId);
}
