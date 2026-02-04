namespace Snakk.Infrastructure.Mappers;

using Snakk.Infrastructure.Database.Entities;
using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Enums;

public static class AchievementMapper
{
    public static Achievement FromPersistence(this AchievementDatabaseEntity entity)
    {
        return Achievement.Rehydrate(
            AchievementId.From(entity.PublicId),
            entity.Slug,
            entity.Name,
            entity.Description,
            entity.IconUrl,
            (AchievementCategoryEnum)entity.CategoryId,
            (AchievementTierEnum)entity.TierLevel,
            entity.Points,
            entity.IsSecret,
            entity.IsActive,
            (AchievementRequirementTypeEnum)entity.RequirementTypeId,
            entity.RequirementConfig,
            entity.DisplayOrder,
            entity.CreatedAt,
            entity.UpdatedAt);
    }

    public static AchievementDatabaseEntity ToPersistence(this Achievement achievement)
    {
        return new AchievementDatabaseEntity
        {
            PublicId = achievement.PublicId,
            Slug = achievement.Slug,
            Name = achievement.Name,
            Description = achievement.Description,
            IconUrl = achievement.IconUrl,
            CategoryId = (int)achievement.Category,
            TierLevel = (int)achievement.Tier,
            Points = achievement.Points,
            IsSecret = achievement.IsSecret,
            IsActive = achievement.IsActive,
            RequirementTypeId = (int)achievement.RequirementType,
            RequirementConfig = achievement.RequirementConfig,
            DisplayOrder = achievement.DisplayOrder,
            CreatedAt = achievement.CreatedAt,
            UpdatedAt = achievement.UpdatedAt
        };
    }
}
