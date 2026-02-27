using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Enums;

namespace Snakk.Domain.Tests.Entities;

public class AchievementTests
{
    #region Create Tests

    [Test]
    public async Task Create_WithValidParams_CreatesAchievement()
    {
        // Act
        var achievement = Achievement.Create(
            "first-post",
            "First Post",
            "Create your first post",
            iconUrl: null,
            AchievementCategoryEnum.Engagement,
            AchievementTierEnum.Bronze,
            points: 10,
            isSecret: false,
            AchievementRequirementTypeEnum.Count,
            "{\"count\":1}",
            displayOrder: 1);

        // Assert
        await Assert.That(achievement).IsNotNull();
        await Assert.That(achievement.PublicId).IsNotNull();
        await Assert.That(achievement.Slug).IsEqualTo("first-post");
        await Assert.That(achievement.Name).IsEqualTo("First Post");
        await Assert.That(achievement.Description).IsEqualTo("Create your first post");
        await Assert.That(achievement.Category).IsEqualTo(AchievementCategoryEnum.Engagement);
        await Assert.That(achievement.Tier).IsEqualTo(AchievementTierEnum.Bronze);
        await Assert.That(achievement.Points).IsEqualTo(10);
        await Assert.That(achievement.IsSecret).IsFalse();
        await Assert.That(achievement.IsActive).IsTrue();
        await Assert.That(achievement.RequirementType).IsEqualTo(AchievementRequirementTypeEnum.Count);
        await Assert.That(achievement.RequirementConfig).IsEqualTo("{\"count\":1}");
        await Assert.That(achievement.DisplayOrder).IsEqualTo(1);
        await Assert.That(achievement.CreatedAt).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public async Task Create_WithEmptySlug_ThrowsArgumentException(string? emptySlug)
    {
        // Act & Assert
        await Assert.That(() => Achievement.Create(
            emptySlug!, "Name", "Desc", null,
            AchievementCategoryEnum.Engagement, AchievementTierEnum.Bronze,
            10, false, AchievementRequirementTypeEnum.Count, "{}", 0)).Throws<ArgumentException>();
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public async Task Create_WithEmptyName_ThrowsArgumentException(string? emptyName)
    {
        // Act & Assert
        await Assert.That(() => Achievement.Create(
            "slug", emptyName!, "Desc", null,
            AchievementCategoryEnum.Engagement, AchievementTierEnum.Bronze,
            10, false, AchievementRequirementTypeEnum.Count, "{}", 0)).Throws<ArgumentException>();
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public async Task Create_WithEmptyDescription_ThrowsArgumentException(string? emptyDesc)
    {
        // Act & Assert
        await Assert.That(() => Achievement.Create(
            "slug", "Name", emptyDesc!, null,
            AchievementCategoryEnum.Engagement, AchievementTierEnum.Bronze,
            10, false, AchievementRequirementTypeEnum.Count, "{}", 0)).Throws<ArgumentException>();
    }

    [Test]
    public async Task Create_WithNegativePoints_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.That(() => Achievement.Create(
            "slug", "Name", "Desc", null,
            AchievementCategoryEnum.Engagement, AchievementTierEnum.Bronze,
            -1, false, AchievementRequirementTypeEnum.Count, "{}", 0)).Throws<ArgumentException>();
    }

    [Test]
    public async Task Create_WithZeroPoints_Succeeds()
    {
        // Act
        var achievement = Achievement.Create(
            "slug", "Name", "Desc", null,
            AchievementCategoryEnum.Engagement, AchievementTierEnum.Bronze,
            0, false, AchievementRequirementTypeEnum.Count, "{}", 0);

        // Assert
        await Assert.That(achievement.Points).IsEqualTo(0);
    }

    #endregion

    #region Deactivate / Activate Tests

    [Test]
    public async Task Deactivate_SetsIsActiveFalse()
    {
        // Arrange
        var achievement = CreateValidAchievement();

        // Act
        achievement.Deactivate();

        // Assert
        await Assert.That(achievement.IsActive).IsFalse();
        await Assert.That(achievement.UpdatedAt).IsNotNull();
    }

    [Test]
    public async Task Activate_SetsIsActiveTrue()
    {
        // Arrange
        var achievement = CreateValidAchievement();
        achievement.Deactivate();

        // Act
        achievement.Activate();

        // Assert
        await Assert.That(achievement.IsActive).IsTrue();
        await Assert.That(achievement.UpdatedAt).IsNotNull();
    }

    #endregion

    #region UpdateDetails Tests

    [Test]
    public async Task UpdateDetails_UpdatesNameDescriptionIconUrl()
    {
        // Arrange
        var achievement = CreateValidAchievement();

        // Act
        achievement.UpdateDetails("New Name", "New Description", "https://example.com/icon.png");

        // Assert
        await Assert.That(achievement.Name).IsEqualTo("New Name");
        await Assert.That(achievement.Description).IsEqualTo("New Description");
        await Assert.That(achievement.IconUrl).IsEqualTo("https://example.com/icon.png");
        await Assert.That(achievement.UpdatedAt).IsNotNull();
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public async Task UpdateDetails_WithEmptyName_ThrowsArgumentException(string? emptyName)
    {
        // Arrange
        var achievement = CreateValidAchievement();

        // Act & Assert
        await Assert.That(() => achievement.UpdateDetails(emptyName!, "Desc", null)).Throws<ArgumentException>();
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("   ")]
    public async Task UpdateDetails_WithEmptyDescription_ThrowsArgumentException(string? emptyDesc)
    {
        // Arrange
        var achievement = CreateValidAchievement();

        // Act & Assert
        await Assert.That(() => achievement.UpdateDetails("Name", emptyDesc!, null)).Throws<ArgumentException>();
    }

    #endregion

    #region UpdateDisplayOrder Tests

    [Test]
    public async Task UpdateDisplayOrder_SetsNewDisplayOrder()
    {
        // Arrange
        var achievement = CreateValidAchievement();

        // Act
        achievement.UpdateDisplayOrder(42);

        // Assert
        await Assert.That(achievement.DisplayOrder).IsEqualTo(42);
        await Assert.That(achievement.UpdatedAt).IsNotNull();
    }

    #endregion

    #region Rehydrate Tests

    [Test]
    public async Task Rehydrate_RestoresAllProperties()
    {
        // Arrange
        var publicId = AchievementId.New();
        var createdAt = DateTime.UtcNow.AddDays(-10);
        var updatedAt = DateTime.UtcNow.AddDays(-1);

        // Act
        var achievement = Achievement.Rehydrate(
            publicId,
            "test-slug",
            "Test Name",
            "Test Description",
            "https://icon.url",
            AchievementCategoryEnum.Social,
            AchievementTierEnum.Gold,
            50,
            isSecret: true,
            isActive: false,
            AchievementRequirementTypeEnum.Streak,
            "{\"days\":7}",
            5,
            createdAt,
            updatedAt);

        // Assert
        await Assert.That(achievement.PublicId).IsEqualTo(publicId);
        await Assert.That(achievement.Slug).IsEqualTo("test-slug");
        await Assert.That(achievement.Name).IsEqualTo("Test Name");
        await Assert.That(achievement.Description).IsEqualTo("Test Description");
        await Assert.That(achievement.IconUrl).IsEqualTo("https://icon.url");
        await Assert.That(achievement.Category).IsEqualTo(AchievementCategoryEnum.Social);
        await Assert.That(achievement.Tier).IsEqualTo(AchievementTierEnum.Gold);
        await Assert.That(achievement.Points).IsEqualTo(50);
        await Assert.That(achievement.IsSecret).IsTrue();
        await Assert.That(achievement.IsActive).IsFalse();
        await Assert.That(achievement.RequirementType).IsEqualTo(AchievementRequirementTypeEnum.Streak);
        await Assert.That(achievement.RequirementConfig).IsEqualTo("{\"days\":7}");
        await Assert.That(achievement.DisplayOrder).IsEqualTo(5);
        await Assert.That(achievement.CreatedAt).IsEqualTo(createdAt);
        await Assert.That(achievement.UpdatedAt).IsEqualTo(updatedAt);
    }

    #endregion

    #region Helpers

    private static Achievement CreateValidAchievement()
    {
        return Achievement.Create(
            "test-slug",
            "Test Achievement",
            "Test Description",
            null,
            AchievementCategoryEnum.Engagement,
            AchievementTierEnum.Bronze,
            10,
            false,
            AchievementRequirementTypeEnum.Count,
            "{\"count\":1}",
            0);
    }

    #endregion
}
