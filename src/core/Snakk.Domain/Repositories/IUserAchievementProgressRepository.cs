namespace Snakk.Domain.Repositories;

using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;

public interface IUserAchievementProgressRepository
{
    Task<UserAchievementProgress?> GetByUserAndAchievementAsync(UserId userId, AchievementId achievementId);
    Task<IEnumerable<UserAchievementProgress>> GetByUserIdAsync(UserId userId);
    Task<IEnumerable<UserAchievementProgress>> GetIncompleteByUserIdAsync(UserId userId);
    Task AddAsync(UserAchievementProgress progress);
    Task UpdateAsync(UserAchievementProgress progress);
    Task DeleteAsync(UserAchievementProgress progress);
}
