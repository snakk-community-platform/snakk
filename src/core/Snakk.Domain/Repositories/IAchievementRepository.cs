namespace Snakk.Domain.Repositories;

using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Enums;

public interface IAchievementRepository
{
    Task<Achievement?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<Achievement?> GetByPublicIdAsync(AchievementId publicId, CancellationToken ct = default);
    Task<IEnumerable<Achievement>> GetByIdsAsync(IEnumerable<AchievementId> ids, CancellationToken ct = default);
    Task<Achievement?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<IEnumerable<Achievement>> GetAllActiveAsync(CancellationToken ct = default);
    Task<IEnumerable<Achievement>> GetByCategoryAsync(AchievementCategoryEnum category, CancellationToken ct = default);
    Task AddAsync(Achievement achievement, CancellationToken ct = default);
    Task UpdateAsync(Achievement achievement, CancellationToken ct = default);
}
