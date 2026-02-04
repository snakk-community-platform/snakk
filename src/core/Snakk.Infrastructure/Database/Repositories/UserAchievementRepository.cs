namespace Snakk.Infrastructure.Database.Repositories;

using Microsoft.EntityFrameworkCore;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;

public class UserAchievementRepository(SnakkDbContext context)
    : GenericDatabaseRepository<UserAchievementDatabaseEntity>(context), IUserAchievementRepository
{
    public override async Task<UserAchievementDatabaseEntity?> GetByIdAsync(int id)
    {
        return await _dbSet
            .Include(ua => ua.Achievement)
            .ThenInclude(a => a.Category)
            .Include(ua => ua.Achievement)
            .ThenInclude(a => a.RequirementType)
            .Include(ua => ua.User)
            .FirstOrDefaultAsync(ua => ua.Id == id);
    }

    public async Task<UserAchievementDatabaseEntity?> GetByPublicIdAsync(string publicId)
    {
        return await _dbSet
            .Include(ua => ua.Achievement)
            .ThenInclude(a => a.Category)
            .Include(ua => ua.Achievement)
            .ThenInclude(a => a.RequirementType)
            .Include(ua => ua.User)
            .FirstOrDefaultAsync(ua => ua.PublicId == publicId);
    }

    public async Task<IEnumerable<UserAchievementDatabaseEntity>> GetByUserIdAsync(int userId)
    {
        return await _dbSet
            .AsNoTracking()
            .Include(ua => ua.Achievement)
            .ThenInclude(a => a.Category)
            .Include(ua => ua.Achievement)
            .ThenInclude(a => a.RequirementType)
            .Where(ua => ua.UserId == userId)
            .OrderByDescending(ua => ua.EarnedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<UserAchievementDatabaseEntity>> GetDisplayedByUserIdAsync(int userId)
    {
        return await _dbSet
            .AsNoTracking()
            .Include(ua => ua.Achievement)
            .ThenInclude(a => a.Category)
            .Include(ua => ua.Achievement)
            .ThenInclude(a => a.RequirementType)
            .Where(ua => ua.UserId == userId && ua.IsDisplayed)
            .OrderBy(ua => ua.DisplayOrder)
            .ToListAsync();
    }

    public async Task<bool> HasAchievementAsync(int userId, int achievementId)
    {
        return await _dbSet
            .AsNoTracking()
            .AnyAsync(ua => ua.UserId == userId && ua.AchievementId == achievementId);
    }
}
