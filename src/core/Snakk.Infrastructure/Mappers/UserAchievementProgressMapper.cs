namespace Snakk.Infrastructure.Mappers;

using Snakk.Infrastructure.Database.Entities;
using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;

public static class UserAchievementProgressMapper
{
    public static UserAchievementProgress FromPersistence(this UserAchievementProgressDatabaseEntity entity)
    {
        return UserAchievementProgress.Rehydrate(
            UserId.From(entity.User.PublicId),
            AchievementId.From(entity.Achievement.PublicId),
            entity.CurrentValue,
            entity.TargetValue,
            entity.ProgressData,
            entity.LastUpdated);
    }

    public static UserAchievementProgressDatabaseEntity ToPersistence(this UserAchievementProgress progress)
    {
        // Note: UserId, AchievementId, and navigation properties (User, Achievement)
        // must be set separately in the repository adapter
        return new UserAchievementProgressDatabaseEntity
        {
            CurrentValue = progress.CurrentValue,
            TargetValue = progress.TargetValue,
            ProgressData = progress.ProgressData,
            LastUpdated = progress.LastUpdated
            // UserId and AchievementId will be set by repository adapter
        };
    }
}
