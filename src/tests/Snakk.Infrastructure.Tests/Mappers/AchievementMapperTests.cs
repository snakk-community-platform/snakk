using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Infrastructure.Mappers;
using Snakk.Shared.Enums;

namespace Snakk.Infrastructure.Tests.Mappers;

public class AchievementMapperTests
{
    #region FromPersistence Tests

    [Test]
    public async Task FromPersistence_MapsAllProperties()
    {
        // Arrange
        var publicId = Guid.NewGuid().ToString();
        var createdAt = new DateTime(2025, 7, 1, 10, 0, 0, DateTimeKind.Utc);
        var updatedAt = new DateTime(2025, 7, 15, 14, 30, 0, DateTimeKind.Utc);

        var entity = new AchievementDatabaseEntity
        {
            Id = 1,
            PublicId = publicId,
            Slug = "first-post",
            Name = "First Post",
            Description = "Create your first post",
            IconUrl = "https://example.com/icons/first-post.png",
            CategoryId = (int)AchievementCategoryEnum.Content,
            TierLevel = (int)AchievementTierEnum.Bronze,
            Points = 10,
            IsSecret = false,
            IsActive = true,
            RequirementTypeId = (int)AchievementRequirementTypeEnum.Count,
            RequirementConfig = "{\"count\":1}",
            DisplayOrder = 5,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };

        // Act
        var achievement = entity.FromPersistence();

        // Assert
        await Assert.That(achievement).IsNotNull();
        await Assert.That(achievement.PublicId.Value).IsEqualTo(publicId);
        await Assert.That(achievement.Slug).IsEqualTo("first-post");
        await Assert.That(achievement.Name).IsEqualTo("First Post");
        await Assert.That(achievement.Description).IsEqualTo("Create your first post");
        await Assert.That(achievement.IconUrl).IsEqualTo("https://example.com/icons/first-post.png");
        await Assert.That(achievement.Category).IsEqualTo(AchievementCategoryEnum.Content);
        await Assert.That(achievement.Tier).IsEqualTo(AchievementTierEnum.Bronze);
        await Assert.That(achievement.Points).IsEqualTo(10);
        await Assert.That(achievement.IsSecret).IsFalse();
        await Assert.That(achievement.IsActive).IsTrue();
        await Assert.That(achievement.RequirementType).IsEqualTo(AchievementRequirementTypeEnum.Count);
        await Assert.That(achievement.RequirementConfig).IsEqualTo("{\"count\":1}");
        await Assert.That(achievement.DisplayOrder).IsEqualTo(5);
        await Assert.That(achievement.CreatedAt).IsEqualTo(createdAt);
        await Assert.That(achievement.UpdatedAt).IsEqualTo(updatedAt);
    }

    [Test]
    public async Task FromPersistence_WithNullIconUrl_MapsCorrectly()
    {
        // Arrange
        var entity = new AchievementDatabaseEntity
        {
            Id = 2,
            PublicId = Guid.NewGuid().ToString(),
            Slug = "secret-achievement",
            Name = "Secret Achievement",
            Description = "A hidden achievement",
            IconUrl = null,
            CategoryId = (int)AchievementCategoryEnum.Special,
            TierLevel = (int)AchievementTierEnum.Gold,
            Points = 50,
            IsSecret = true,
            IsActive = true,
            RequirementTypeId = (int)AchievementRequirementTypeEnum.Milestone,
            RequirementConfig = "{\"milestone\":\"secret\"}",
            DisplayOrder = 99,
            CreatedAt = DateTime.UtcNow
        };

        // Act
        var achievement = entity.FromPersistence();

        // Assert
        await Assert.That(achievement.IconUrl).IsNull();
    }

    [Test]
    public async Task FromPersistence_WithNullUpdatedAt_MapsCorrectly()
    {
        // Arrange
        var entity = new AchievementDatabaseEntity
        {
            Id = 3,
            PublicId = Guid.NewGuid().ToString(),
            Slug = "new-achievement",
            Name = "New Achievement",
            Description = "Never updated",
            CategoryId = (int)AchievementCategoryEnum.Engagement,
            TierLevel = (int)AchievementTierEnum.Bronze,
            Points = 5,
            IsSecret = false,
            IsActive = true,
            RequirementTypeId = (int)AchievementRequirementTypeEnum.Count,
            RequirementConfig = "{\"count\":5}",
            DisplayOrder = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = null
        };

        // Act
        var achievement = entity.FromPersistence();

        // Assert
        await Assert.That(achievement.UpdatedAt).IsNull();
    }

    [Test]
    public async Task FromPersistence_MapsEnumsCorrectly()
    {
        // Arrange - use specific enum values to verify casting works
        var entity = new AchievementDatabaseEntity
        {
            Id = 4,
            PublicId = Guid.NewGuid().ToString(),
            Slug = "social-diamond",
            Name = "Social Diamond",
            Description = "Top social achievement",
            CategoryId = (int)AchievementCategoryEnum.Social,
            TierLevel = (int)AchievementTierEnum.Diamond,
            Points = 500,
            IsSecret = false,
            IsActive = true,
            RequirementTypeId = (int)AchievementRequirementTypeEnum.TimeBased,
            RequirementConfig = "{\"days\":365}",
            DisplayOrder = 0,
            CreatedAt = DateTime.UtcNow
        };

        // Act
        var achievement = entity.FromPersistence();

        // Assert
        await Assert.That(achievement.Category).IsEqualTo(AchievementCategoryEnum.Social);
        await Assert.That(achievement.Tier).IsEqualTo(AchievementTierEnum.Diamond);
        await Assert.That(achievement.RequirementType).IsEqualTo(AchievementRequirementTypeEnum.TimeBased);
    }

    #endregion

    #region ToPersistence Tests

    [Test]
    public async Task ToPersistence_MapsAllProperties()
    {
        // Arrange
        var achievementId = AchievementId.New();
        var createdAt = new DateTime(2025, 6, 20, 9, 0, 0, DateTimeKind.Utc);
        var updatedAt = new DateTime(2025, 7, 5, 12, 0, 0, DateTimeKind.Utc);

        var achievement = Achievement.Rehydrate(
            achievementId,
            "milestone-100",
            "Century Club",
            "Reach 100 posts",
            "https://example.com/icons/century.png",
            AchievementCategoryEnum.Milestones,
            AchievementTierEnum.Platinum,
            200,
            false,
            true,
            AchievementRequirementTypeEnum.Count,
            "{\"count\":100}",
            10,
            createdAt,
            updatedAt);

        // Act
        var entity = achievement.ToPersistence();

        // Assert
        await Assert.That(entity).IsNotNull();
        await Assert.That(entity.PublicId).IsEqualTo(achievementId.Value);
        await Assert.That(entity.Slug).IsEqualTo("milestone-100");
        await Assert.That(entity.Name).IsEqualTo("Century Club");
        await Assert.That(entity.Description).IsEqualTo("Reach 100 posts");
        await Assert.That(entity.IconUrl).IsEqualTo("https://example.com/icons/century.png");
        await Assert.That(entity.CategoryId).IsEqualTo((int)AchievementCategoryEnum.Milestones);
        await Assert.That(entity.TierLevel).IsEqualTo((int)AchievementTierEnum.Platinum);
        await Assert.That(entity.Points).IsEqualTo(200);
        await Assert.That(entity.IsSecret).IsFalse();
        await Assert.That(entity.IsActive).IsTrue();
        await Assert.That(entity.RequirementTypeId).IsEqualTo((int)AchievementRequirementTypeEnum.Count);
        await Assert.That(entity.RequirementConfig).IsEqualTo("{\"count\":100}");
        await Assert.That(entity.DisplayOrder).IsEqualTo(10);
        await Assert.That(entity.CreatedAt).IsEqualTo(createdAt);
        await Assert.That(entity.UpdatedAt).IsEqualTo(updatedAt);
    }

    [Test]
    public async Task ToPersistence_CastsEnumsToIntegers()
    {
        // Arrange
        var achievement = Achievement.Rehydrate(
            AchievementId.New(),
            "moderation-streak",
            "Moderation Streak",
            "Moderate consistently",
            null,
            AchievementCategoryEnum.Moderation,
            AchievementTierEnum.Silver,
            25,
            false,
            true,
            AchievementRequirementTypeEnum.Streak,
            "{\"streak\":7}",
            3,
            DateTime.UtcNow);

        // Act
        var entity = achievement.ToPersistence();

        // Assert - verify enums are stored as integers
        await Assert.That(entity.CategoryId).IsEqualTo(5); // Moderation = 5
        await Assert.That(entity.TierLevel).IsEqualTo(2);  // Silver = 2
        await Assert.That(entity.RequirementTypeId).IsEqualTo(2); // Streak = 2
    }

    [Test]
    public async Task ToPersistence_WithNullIconUrl_MapsCorrectly()
    {
        // Arrange
        var achievement = Achievement.Rehydrate(
            AchievementId.New(),
            "no-icon",
            "No Icon Achievement",
            "Achievement without an icon",
            null,
            AchievementCategoryEnum.Engagement,
            AchievementTierEnum.Bronze,
            5,
            false,
            true,
            AchievementRequirementTypeEnum.Count,
            "{\"count\":1}",
            0,
            DateTime.UtcNow);

        // Act
        var entity = achievement.ToPersistence();

        // Assert
        await Assert.That(entity.IconUrl).IsNull();
    }

    #endregion

    #region Round-Trip Tests

    [Test]
    public async Task RoundTrip_PreservesAllData()
    {
        // Arrange
        var achievementId = AchievementId.New();
        var createdAt = new DateTime(2025, 5, 10, 8, 0, 0, DateTimeKind.Utc);
        var updatedAt = new DateTime(2025, 6, 1, 16, 45, 0, DateTimeKind.Utc);

        var original = Achievement.Rehydrate(
            achievementId,
            "round-trip-test",
            "Round Trip Achievement",
            "Tests that data survives round-trip mapping",
            "https://example.com/icons/round-trip.png",
            AchievementCategoryEnum.Content,
            AchievementTierEnum.Gold,
            100,
            true,
            true,
            AchievementRequirementTypeEnum.Milestone,
            "{\"milestone\":\"round-trip\"}",
            7,
            createdAt,
            updatedAt);

        // Act
        var entity = original.ToPersistence();
        var reconstructed = entity.FromPersistence();

        // Assert
        await Assert.That(reconstructed.PublicId).IsEqualTo(original.PublicId);
        await Assert.That(reconstructed.Slug).IsEqualTo(original.Slug);
        await Assert.That(reconstructed.Name).IsEqualTo(original.Name);
        await Assert.That(reconstructed.Description).IsEqualTo(original.Description);
        await Assert.That(reconstructed.IconUrl).IsEqualTo(original.IconUrl);
        await Assert.That(reconstructed.Category).IsEqualTo(original.Category);
        await Assert.That(reconstructed.Tier).IsEqualTo(original.Tier);
        await Assert.That(reconstructed.Points).IsEqualTo(original.Points);
        await Assert.That(reconstructed.IsSecret).IsEqualTo(original.IsSecret);
        await Assert.That(reconstructed.IsActive).IsEqualTo(original.IsActive);
        await Assert.That(reconstructed.RequirementType).IsEqualTo(original.RequirementType);
        await Assert.That(reconstructed.RequirementConfig).IsEqualTo(original.RequirementConfig);
        await Assert.That(reconstructed.DisplayOrder).IsEqualTo(original.DisplayOrder);
        await Assert.That(reconstructed.CreatedAt).IsEqualTo(original.CreatedAt);
        await Assert.That(reconstructed.UpdatedAt).IsEqualTo(original.UpdatedAt);
    }

    [Test]
    public async Task RoundTrip_WithAllEnumValues_PreservesData()
    {
        // Test with different enum combinations to ensure casting is symmetric
        var testCases = new[]
        {
            (AchievementCategoryEnum.Engagement, AchievementTierEnum.Bronze, AchievementRequirementTypeEnum.Count),
            (AchievementCategoryEnum.Social, AchievementTierEnum.Silver, AchievementRequirementTypeEnum.Streak),
            (AchievementCategoryEnum.Content, AchievementTierEnum.Gold, AchievementRequirementTypeEnum.Milestone),
            (AchievementCategoryEnum.Milestones, AchievementTierEnum.Platinum, AchievementRequirementTypeEnum.TimeBased),
            (AchievementCategoryEnum.Moderation, AchievementTierEnum.Diamond, AchievementRequirementTypeEnum.Count),
            (AchievementCategoryEnum.Special, AchievementTierEnum.Bronze, AchievementRequirementTypeEnum.Streak),
        };

        foreach (var (category, tier, requirementType) in testCases)
        {
            var original = Achievement.Rehydrate(
                AchievementId.New(),
                $"enum-test-{(int)category}-{(int)tier}-{(int)requirementType}",
                $"Enum Test {category} {tier} {requirementType}",
                "Testing enum round-trip",
                null,
                category,
                tier,
                10,
                false,
                true,
                requirementType,
                "{}",
                0,
                DateTime.UtcNow);

            var entity = original.ToPersistence();
            var reconstructed = entity.FromPersistence();

            await Assert.That(reconstructed.Category).IsEqualTo(category);
            await Assert.That(reconstructed.Tier).IsEqualTo(tier);
            await Assert.That(reconstructed.RequirementType).IsEqualTo(requirementType);
        }
    }

    #endregion
}
