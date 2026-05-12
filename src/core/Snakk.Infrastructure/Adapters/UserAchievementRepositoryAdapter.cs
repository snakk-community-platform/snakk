namespace Snakk.Infrastructure.Adapters;

using Microsoft.EntityFrameworkCore;
using Snakk.Infrastructure.Database;
using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Mappers;

public class UserAchievementRepositoryAdapter(
    Infrastructure.Database.Repositories.IUserAchievementRepository databaseRepository,
    SnakkDbContext context) : Domain.Repositories.IUserAchievementRepository
{
    public async Task<UserAchievement?> GetByIdAsync(int id)
    {
        var projection = await context.UserAchievements
            .Where(ua => ua.Id == id)
            .Select(ua => new UserAchievementProjection(
                ua.PublicId, ua.UserPublicId, ua.AchievementPublicId,
                ua.EarnedAt, ua.IsDisplayed, ua.DisplayOrder, ua.NotificationSent))
            .FirstOrDefaultAsync();
        return projection?.ToDomain();
    }

    public async Task<UserAchievement?> GetByPublicIdAsync(UserAchievementId publicId)
    {
        var projection = await context.UserAchievements
            .Where(ua => ua.PublicId == publicId.Value)
            .Select(ua => new UserAchievementProjection(
                ua.PublicId, ua.UserPublicId, ua.AchievementPublicId,
                ua.EarnedAt, ua.IsDisplayed, ua.DisplayOrder, ua.NotificationSent))
            .FirstOrDefaultAsync();
        return projection?.ToDomain();
    }

    public async Task<IEnumerable<UserAchievement>> GetByUserIdAsync(UserId userId)
    {
        var projections = await context.UserAchievements
            .Where(ua => ua.UserPublicId == userId.Value)
            .Select(ua => new UserAchievementProjection(
                ua.PublicId, ua.UserPublicId, ua.AchievementPublicId,
                ua.EarnedAt, ua.IsDisplayed, ua.DisplayOrder, ua.NotificationSent))
            .ToListAsync();

        return projections.Select(p => p.ToDomain());
    }

    public async Task<IEnumerable<UserAchievement>> GetDisplayedByUserIdAsync(UserId userId)
    {
        var projections = await context.UserAchievements
            .Where(ua =>
                ua.UserPublicId == userId.Value
                && ua.IsDisplayed)
            .OrderBy(ua => ua.DisplayOrder)
            .Select(ua => new UserAchievementProjection(
                ua.PublicId, ua.UserPublicId, ua.AchievementPublicId,
                ua.EarnedAt, ua.IsDisplayed, ua.DisplayOrder, ua.NotificationSent))
            .ToListAsync();

        return projections.Select(p => p.ToDomain());
    }

    public async Task<bool> HasAchievementAsync(UserId userId, AchievementId achievementId)
    {
        var user = await context.Users
            .FirstOrDefaultAsync(u => u.PublicId == userId.Value);

        if (user is null)
            return false;

        var achievement = await context.Achievements
            .FirstOrDefaultAsync(a => a.PublicId == achievementId.Value);

        if (achievement is null)
            return false;

        return await databaseRepository.HasAchievementAsync(user.Id, achievement.Id);
    }

    public async Task AddAsync(UserAchievement userAchievement)
    {
        var entity = userAchievement.ToPersistence();

        // Resolve foreign keys from PublicIds
        var user = await context.Users.FirstOrDefaultAsync(u => u.PublicId == userAchievement.UserId.Value);

        if (user is null)
            throw new InvalidOperationException($"User with PublicId '{userAchievement.UserId}' not found");

        var achievement = await context.Achievements.FirstOrDefaultAsync(a => a.PublicId == userAchievement.AchievementId.Value);

        if (achievement is null)
            throw new InvalidOperationException($"Achievement with PublicId '{userAchievement.AchievementId}' not found");

        entity.UserId = user.Id;
        entity.UserPublicId = user.PublicId;
        entity.AchievementId = achievement.Id;
        entity.AchievementPublicId = achievement.PublicId;

        await databaseRepository.AddAsync(entity);
        await databaseRepository.SaveChangesAsync();
    }

    public async Task UpdateAsync(UserAchievement userAchievement)
    {
        var entity = await context.UserAchievements
            .FirstOrDefaultAsync(ua => ua.PublicId == userAchievement.PublicId.Value);

        if (entity is null)
            throw new InvalidOperationException($"UserAchievement with PublicId '{userAchievement.PublicId}' not found");

        // Update properties
        entity.IsDisplayed = userAchievement.IsDisplayed;
        entity.DisplayOrder = userAchievement.DisplayOrder;
        entity.NotificationSent = userAchievement.NotificationSent;

        await databaseRepository.UpdateAsync(entity);
        await databaseRepository.SaveChangesAsync();
    }

    private record UserAchievementProjection(
        string PublicId,
        string UserPublicId,
        string AchievementPublicId,
        DateTime EarnedAt,
        bool IsDisplayed,
        int DisplayOrder,
        bool NotificationSent)
    {
        public UserAchievement ToDomain() => UserAchievement.Rehydrate(
            UserAchievementId.From(PublicId),
            UserId.From(UserPublicId),
            AchievementId.From(AchievementPublicId),
            EarnedAt, IsDisplayed, DisplayOrder, NotificationSent);
    }
}
