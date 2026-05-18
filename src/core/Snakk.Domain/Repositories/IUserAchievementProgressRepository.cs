namespace Snakk.Domain.Repositories;

using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;

public interface IUserAchievementProgressRepository
{
    Task<UserAchievementProgress?> GetByUserAndAchievementAsync(UserId userId, AchievementId achievementId, CancellationToken ct = default);
    Task<IEnumerable<UserAchievementProgress>> GetByUserIdAsync(UserId userId, CancellationToken ct = default);
    Task<IEnumerable<UserAchievementProgress>> GetIncompleteByUserIdAsync(UserId userId, CancellationToken ct = default);
    Task AddAsync(UserAchievementProgress progress, CancellationToken ct = default);
    Task UpdateAsync(UserAchievementProgress progress, CancellationToken ct = default);
    Task DeleteAsync(UserAchievementProgress progress, CancellationToken ct = default);
}
