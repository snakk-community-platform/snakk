namespace Snakk.Domain.Entities;

using Snakk.Domain.ValueObjects;
using Snakk.Shared.Enums;

public class Achievement
{
    public AchievementId PublicId { get; private set; }
    public string Slug { get; private set; }
    public string Name { get; private set; }
    public string Description { get; private set; }
    public string? IconUrl { get; private set; }
    public AchievementCategoryEnum Category { get; private set; }
    public AchievementTierEnum Tier { get; private set; }
    public int Points { get; private set; }
    public bool IsSecret { get; private set; }
    public bool IsActive { get; private set; }
    public AchievementRequirementTypeEnum RequirementType { get; private set; }
    public string RequirementConfig { get; private set; } // JSON configuration
    public int DisplayOrder { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

#pragma warning disable CS8618 // Non-nullable property must contain a non-null value when exiting constructor
    private Achievement() { }
#pragma warning restore CS8618

    private Achievement(
        AchievementId publicId,
        string slug,
        string name,
        string description,
        string? iconUrl,
        AchievementCategoryEnum category,
        AchievementTierEnum tier,
        int points,
        bool isSecret,
        bool isActive,
        AchievementRequirementTypeEnum requirementType,
        string requirementConfig,
        int displayOrder,
        DateTime createdAt,
        DateTime? updatedAt = null)
    {
        PublicId = publicId;
        Slug = slug;
        Name = name;
        Description = description;
        IconUrl = iconUrl;
        Category = category;
        Tier = tier;
        Points = points;
        IsSecret = isSecret;
        IsActive = isActive;
        RequirementType = requirementType;
        RequirementConfig = requirementConfig;
        DisplayOrder = displayOrder;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public static Achievement Create(
        string slug,
        string name,
        string description,
        string? iconUrl,
        AchievementCategoryEnum category,
        AchievementTierEnum tier,
        int points,
        bool isSecret,
        AchievementRequirementTypeEnum requirementType,
        string requirementConfig,
        int displayOrder)
    {
        if (string.IsNullOrWhiteSpace(slug))
            throw new ArgumentException("Achievement slug cannot be empty", nameof(slug));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Achievement name cannot be empty", nameof(name));

        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Achievement description cannot be empty", nameof(description));

        if (string.IsNullOrWhiteSpace(requirementConfig))
            throw new ArgumentException("Achievement requirement config cannot be empty", nameof(requirementConfig));

        if (points < 0)
            throw new ArgumentException("Achievement points cannot be negative", nameof(points));

        return new Achievement(
            AchievementId.New(),
            slug,
            name,
            description,
            iconUrl,
            category,
            tier,
            points,
            isSecret,
            isActive: true,
            requirementType,
            requirementConfig,
            displayOrder,
            DateTime.UtcNow);
    }

    public static Achievement Rehydrate(
        AchievementId publicId,
        string slug,
        string name,
        string description,
        string? iconUrl,
        AchievementCategoryEnum category,
        AchievementTierEnum tier,
        int points,
        bool isSecret,
        bool isActive,
        AchievementRequirementTypeEnum requirementType,
        string requirementConfig,
        int displayOrder,
        DateTime createdAt,
        DateTime? updatedAt = null)
    {
        return new Achievement(
            publicId,
            slug,
            name,
            description,
            iconUrl,
            category,
            tier,
            points,
            isSecret,
            isActive,
            requirementType,
            requirementConfig,
            displayOrder,
            createdAt,
            updatedAt);
    }

    public void UpdateDetails(string name, string description, string? iconUrl)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Achievement name cannot be empty", nameof(name));

        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Achievement description cannot be empty", nameof(description));

        Name = name;
        Description = description;
        IconUrl = iconUrl;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Activate()
    {
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateDisplayOrder(int displayOrder)
    {
        DisplayOrder = displayOrder;
        UpdatedAt = DateTime.UtcNow;
    }
}
