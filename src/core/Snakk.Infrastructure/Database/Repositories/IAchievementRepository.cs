namespace Snakk.Infrastructure.Database.Repositories;

using Snakk.Infrastructure.Database.Entities;

public interface IAchievementRepository : IGenericDatabaseRepository<AchievementDatabaseEntity>
{
    Task<AchievementDatabaseEntity?> GetByPublicIdAsync(string publicId);
    Task<AchievementDatabaseEntity?> GetBySlugAsync(string slug);
    Task<IEnumerable<AchievementDatabaseEntity>> GetAllActiveAsync();
    Task<IEnumerable<AchievementDatabaseEntity>> GetByCategoryIdAsync(int categoryId);
}
