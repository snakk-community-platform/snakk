namespace Snakk.Infrastructure.Database.Repositories;

using Microsoft.EntityFrameworkCore;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;

public class AchievementRepository(SnakkDbContext context)
    : GenericDatabaseRepository<AchievementDatabaseEntity>(context), IAchievementRepository
{
    public override async Task<AchievementDatabaseEntity?> GetByIdAsync(int id)
    {
        return await _dbSet
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    public async Task<AchievementDatabaseEntity?> GetByPublicIdAsync(string publicId)
    {
        return await _dbSet
            .FirstOrDefaultAsync(a => a.PublicId == publicId);
    }

    public async Task<AchievementDatabaseEntity?> GetBySlugAsync(string slug)
    {
        return await _dbSet
            .FirstOrDefaultAsync(a => a.Slug == slug);
    }

    public async Task<IEnumerable<AchievementDatabaseEntity>> GetAllActiveAsync()
    {
        return await _dbSet
            .Where(a => a.IsActive)
            .OrderBy(a => a.DisplayOrder)
            .ToListAsync();
    }

    public async Task<IEnumerable<AchievementDatabaseEntity>> GetByCategoryIdAsync(int categoryId)
    {
        return await _dbSet
            .Where(a => a.CategoryId == categoryId && a.IsActive)
            .OrderBy(a => a.DisplayOrder)
            .ToListAsync();
    }
}
