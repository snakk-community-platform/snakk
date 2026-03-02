namespace Snakk.Infrastructure.Database.Repositories;

using Microsoft.EntityFrameworkCore;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;

public class UserAchievementProgressRepository(SnakkDbContext context)
    : GenericDatabaseRepository<UserAchievementProgressDatabaseEntity>(context), IUserAchievementProgressRepository
{
    public async Task<UserAchievementProgressDatabaseEntity?> GetByUserAndAchievementAsync(int userId, int achievementId)
    {
        return await _dbSet
            .FirstOrDefaultAsync(p => p.UserId == userId && p.AchievementId == achievementId);
    }

    public async Task<IEnumerable<UserAchievementProgressDatabaseEntity>> GetByUserIdAsync(int userId)
    {
        return await _dbSet
            .Where(p => p.UserId == userId)
            .ToListAsync();
    }

    public async Task<IEnumerable<UserAchievementProgressDatabaseEntity>> GetIncompleteByUserIdAsync(int userId)
    {
        return await _dbSet
            .Where(p => p.UserId == userId && p.CurrentValue < p.TargetValue)
            .ToListAsync();
    }
}
