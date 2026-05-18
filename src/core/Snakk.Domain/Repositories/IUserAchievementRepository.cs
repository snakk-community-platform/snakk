namespace Snakk.Domain.Repositories;

using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;

public interface IUserAchievementRepository
{
    Task<UserAchievement?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<UserAchievement?> GetByPublicIdAsync(UserAchievementId publicId, CancellationToken ct = default);
    Task<IEnumerable<UserAchievement>> GetByUserIdAsync(UserId userId, CancellationToken ct = default);
    Task<IEnumerable<UserAchievement>> GetDisplayedByUserIdAsync(UserId userId, CancellationToken ct = default);
    Task<bool> HasAchievementAsync(UserId userId, AchievementId achievementId, CancellationToken ct = default);
    Task AddAsync(UserAchievement userAchievement, CancellationToken ct = default);
    Task UpdateAsync(UserAchievement userAchievement, CancellationToken ct = default);
}
