namespace Snakk.Infrastructure.Database.Repositories;

using Microsoft.EntityFrameworkCore;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;

public class UserAchievementProgressRepository(SnakkDbContext context)
    : GenericDatabaseRepository<UserAchievementProgressDatabaseEntity>(context), IUserAchievementProgressRepository
{
    public async Task<UserAchievementProgressDatabaseEntity?> GetByUserAndAchievementAsync(
        int userId,
        int achievementId,
        CancellationToken ct = default) => await _dbSet
        .FirstOrDefaultAsync(p =>
            p.UserId == userId
            && p.AchievementId == achievementId, ct);

    public async Task<IEnumerable<UserAchievementProgressDatabaseEntity>> GetByUserIdAsync(int userId, CancellationToken ct = default) => await _dbSet
        .Where(p => p.UserId == userId)
        .ToListAsync(ct);

    public async Task<IEnumerable<UserAchievementProgressDatabaseEntity>> GetIncompleteByUserIdAsync(int userId, CancellationToken ct = default) => await _dbSet
        .Where(p => p.UserId == userId && p.CurrentValue < p.TargetValue)
        .ToListAsync(ct);
}
