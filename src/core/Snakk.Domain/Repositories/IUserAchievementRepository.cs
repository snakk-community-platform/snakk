namespace Snakk.Domain.Repositories;

using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;

public interface IUserAchievementRepository
{
    Task<UserAchievement?> GetByIdAsync(int id);
    Task<UserAchievement?> GetByPublicIdAsync(UserAchievementId publicId);
    Task<IEnumerable<UserAchievement>> GetByUserIdAsync(UserId userId);
    Task<IEnumerable<UserAchievement>> GetDisplayedByUserIdAsync(UserId userId);
    Task<bool> HasAchievementAsync(UserId userId, AchievementId achievementId);
    Task AddAsync(UserAchievement userAchievement);
    Task UpdateAsync(UserAchievement userAchievement);
}
