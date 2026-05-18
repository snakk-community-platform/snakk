namespace Snakk.Infrastructure.Database.Repositories;

using Microsoft.EntityFrameworkCore;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;

public class AchievementRepository(SnakkDbContext context)
    : GenericDatabaseRepository<AchievementDatabaseEntity>(context), IAchievementRepository
{
    public override async Task<AchievementDatabaseEntity?> GetByIdAsync(int id, CancellationToken ct = default) =>
        await _dbSet.FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<AchievementDatabaseEntity?> GetByPublicIdAsync(string publicId, CancellationToken ct = default) =>
        await _dbSet.FirstOrDefaultAsync(a => a.PublicId == publicId, ct);

    public async Task<AchievementDatabaseEntity?> GetBySlugAsync(string slug, CancellationToken ct = default) =>
        await _dbSet.FirstOrDefaultAsync(a => a.Slug == slug, ct);

    public async Task<IEnumerable<AchievementDatabaseEntity>> GetAllActiveAsync(CancellationToken ct = default) => await _dbSet
        .Where(a => a.IsActive)
        .OrderBy(a => a.DisplayOrder)
        .ToListAsync(ct);

    public async Task<IEnumerable<AchievementDatabaseEntity>> GetByCategoryIdAsync(int categoryId, CancellationToken ct = default) => await _dbSet
        .Where(a => a.CategoryId == categoryId && a.IsActive)
        .OrderBy(a => a.DisplayOrder)
        .ToListAsync(ct);
}
