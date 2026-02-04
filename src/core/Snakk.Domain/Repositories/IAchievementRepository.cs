namespace Snakk.Domain.Repositories;

using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Enums;

public interface IAchievementRepository
{
    Task<Achievement?> GetByIdAsync(int id);
    Task<Achievement?> GetByPublicIdAsync(AchievementId publicId);
    Task<Achievement?> GetBySlugAsync(string slug);
    Task<IEnumerable<Achievement>> GetAllActiveAsync();
    Task<IEnumerable<Achievement>> GetByCategoryAsync(AchievementCategoryEnum category);
    Task AddAsync(Achievement achievement);
    Task UpdateAsync(Achievement achievement);
}
