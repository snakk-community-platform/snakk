namespace Snakk.Infrastructure.Database.Repositories;

using Microsoft.EntityFrameworkCore;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;

public class UserAchievementRepository(SnakkDbContext context)
    : GenericDatabaseRepository<UserAchievementDatabaseEntity>(context), IUserAchievementRepository
{
    public async Task<UserAchievementDatabaseEntity?> GetByPublicIdAsync(string publicId) =>
        await _dbSet.FirstOrDefaultAsync(ua => ua.PublicId == publicId);

    public async Task<IEnumerable<UserAchievementDatabaseEntity>> GetByUserIdAsync(int userId) => await _dbSet
        .Where(ua => ua.UserId == userId)
        .OrderByDescending(ua => ua.EarnedAt)
        .ToListAsync();

    public async Task<IEnumerable<UserAchievementDatabaseEntity>> GetDisplayedByUserIdAsync(int userId) => await _dbSet
        .Where(ua => ua.UserId == userId && ua.IsDisplayed)
        .OrderBy(ua => ua.DisplayOrder)
        .ToListAsync();

    public async Task<bool> HasAchievementAsync(int userId, int achievementId) => await _dbSet
        .AnyAsync(ua =>
            ua.UserId == userId
            && ua.AchievementId == achievementId);
}
