namespace Snakk.Infrastructure.Mappers;

using Snakk.Infrastructure.Database.Entities;
using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;

public static class UserAchievementMapper
{
    public static UserAchievement FromPersistence(this UserAchievementDatabaseEntity entity)
    {
        return UserAchievement.Rehydrate(
            UserAchievementId.From(entity.PublicId),
            UserId.From(entity.User.PublicId),
            AchievementId.From(entity.Achievement.PublicId),
            entity.EarnedAt,
            entity.IsDisplayed,
            entity.DisplayOrder,
            entity.NotificationSent);
    }

    public static UserAchievementDatabaseEntity ToPersistence(this UserAchievement userAchievement)
    {
        // Note: UserId, AchievementId, and navigation properties (User, Achievement)
        // must be set separately in the repository adapter
        return new UserAchievementDatabaseEntity
        {
            PublicId = userAchievement.PublicId,
            EarnedAt = userAchievement.EarnedAt,
            IsDisplayed = userAchievement.IsDisplayed,
            DisplayOrder = userAchievement.DisplayOrder,
            NotificationSent = userAchievement.NotificationSent
            // UserId and AchievementId will be set by repository adapter
        };
    }
}
