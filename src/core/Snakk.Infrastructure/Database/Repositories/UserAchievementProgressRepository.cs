namespace Snakk.Infrastructure.Database.Repositories;

using Microsoft.EntityFrameworkCore;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;

public class UserAchievementProgressRepository(SnakkDbContext context)
    : GenericDatabaseRepository<UserAchievementProgressDatabaseEntity>(context), IUserAchievementProgressRepository
{
    public override async Task<UserAchievementProgressDatabaseEntity?> GetByIdAsync(int id)
    {
        return await _dbSet
            .Include(p => p.Achievement)
            .ThenInclude(a => a.Category)
            .Include(p => p.Achievement)
            .ThenInclude(a => a.RequirementType)
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<UserAchievementProgressDatabaseEntity?> GetByUserAndAchievementAsync(int userId, int achievementId)
    {
        return await _dbSet
            .Include(p => p.Achievement)
            .ThenInclude(a => a.Category)
            .Include(p => p.Achievement)
            .ThenInclude(a => a.RequirementType)
            .FirstOrDefaultAsync(p => p.UserId == userId && p.AchievementId == achievementId);
    }

    public async Task<IEnumerable<UserAchievementProgressDatabaseEntity>> GetByUserIdAsync(int userId)
    {
        return await _dbSet
            .AsNoTracking()
            .Include(p => p.Achievement)
            .ThenInclude(a => a.Category)
            .Include(p => p.Achievement)
            .ThenInclude(a => a.RequirementType)
            .Where(p => p.UserId == userId)
            .ToListAsync();
    }

    public async Task<IEnumerable<UserAchievementProgressDatabaseEntity>> GetIncompleteByUserIdAsync(int userId)
    {
        return await _dbSet
            .AsNoTracking()
            .Include(p => p.Achievement)
            .ThenInclude(a => a.Category)
            .Include(p => p.Achievement)
            .ThenInclude(a => a.RequirementType)
            .Where(p => p.UserId == userId && p.CurrentValue < p.TargetValue)
            .ToListAsync();
    }
}
