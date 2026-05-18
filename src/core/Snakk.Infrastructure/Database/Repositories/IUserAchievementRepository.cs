namespace Snakk.Infrastructure.Database.Repositories;

using Snakk.Infrastructure.Database.Entities;

public interface IUserAchievementRepository : IGenericDatabaseRepository<UserAchievementDatabaseEntity>
{
    Task<UserAchievementDatabaseEntity?> GetByPublicIdAsync(string publicId, CancellationToken ct = default);
    Task<IEnumerable<UserAchievementDatabaseEntity>> GetByUserIdAsync(int userId, CancellationToken ct = default);
    Task<IEnumerable<UserAchievementDatabaseEntity>> GetDisplayedByUserIdAsync(int userId, CancellationToken ct = default);
    Task<bool> HasAchievementAsync(int userId, int achievementId, CancellationToken ct = default);
}
