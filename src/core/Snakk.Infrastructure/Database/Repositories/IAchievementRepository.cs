namespace Snakk.Infrastructure.Database.Repositories;

using Snakk.Infrastructure.Database.Entities;

public interface IAchievementRepository : IGenericDatabaseRepository<AchievementDatabaseEntity>
{
    Task<AchievementDatabaseEntity?> GetByPublicIdAsync(string publicId, CancellationToken ct = default);
    Task<AchievementDatabaseEntity?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<IEnumerable<AchievementDatabaseEntity>> GetAllActiveAsync(CancellationToken ct = default);
    Task<IEnumerable<AchievementDatabaseEntity>> GetByCategoryIdAsync(int categoryId, CancellationToken ct = default);
}
