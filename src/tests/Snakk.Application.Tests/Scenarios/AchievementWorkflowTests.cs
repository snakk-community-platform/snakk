using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Enums;

namespace Snakk.Application.Tests.Scenarios;

/// <summary>
/// Workflow tests for achievement and progress tracking scenarios.
/// These tests verify the domain entities used in achievement workflows.
/// </summary>
public class AchievementWorkflowTests
{
    #region User Creates Posts -> Progress Increments -> Achievement Awarded

    [Test]
    public async Task Workflow_UserCreatesMultiplePosts_ProgressIncrements_AchievementAwarded()
    {
        // Step 1: Create an achievement for "First 10 Posts"
        var achievement = Achievement.Create(
            "first-10-posts",
            "Contributor",
            "Create 10 posts in any discussion",
            iconUrl: null,
            AchievementCategoryEnum.Content,
            AchievementTierEnum.Bronze,
            points: 50,
            isSecret: false,
            AchievementRequirementTypeEnum.Count,
            "{\"targetCount\": 10}",
            displayOrder: 1);

        await Assert.That(achievement.IsActive).IsTrue();

        // Step 2: Create progress tracker for user
        var userId = UserId.New();
        var progress = UserAchievementProgress.Create(userId, achievement.PublicId, targetValue: 10);

        await Assert.That(progress.CurrentValue).IsEqualTo(0);
        await Assert.That(progress.IsComplete()).IsFalse();

        // Step 3: User creates posts, progress increments
        for (var i = 0; i < 10; i++)
        {
            progress.IncrementProgress();
        }

        // Step 4: Check progress is complete
        await Assert.That(progress.CurrentValue).IsEqualTo(10);
        await Assert.That(progress.IsComplete()).IsTrue();
        await Assert.That(progress.GetProgressPercentage()).IsEqualTo(100.0);

        // Step 5: Award the achievement
        var userAchievement = UserAchievement.Create(userId, achievement.PublicId);

        await Assert.That(userAchievement.UserId).IsEqualTo(userId);
        await Assert.That(userAchievement.AchievementId).IsEqualTo(achievement.PublicId);
        await Assert.That(userAchievement.EarnedAt).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
        await Assert.That(userAchievement.IsDisplayed).IsFalse();
        await Assert.That(userAchievement.NotificationSent).IsFalse();
    }

    [Test]
    public async Task Workflow_UserCreatesPostsPartially_AchievementNotYetAwarded()
    {
        // Arrange
        var userId = UserId.New();
        var achievementId = AchievementId.New();
        var progress = UserAchievementProgress.Create(userId, achievementId, targetValue: 50);

        // Act - user creates 25 posts
        for (var i = 0; i < 25; i++)
        {
            progress.IncrementProgress();
        }

        // Assert - not yet complete
        await Assert.That(progress.CurrentValue).IsEqualTo(25);
        await Assert.That(progress.IsComplete()).IsFalse();
        await Assert.That(progress.GetProgressPercentage()).IsEqualTo(50.0);
    }

    #endregion

    #region User Achievement Progress Tracking

    [Test]
    public async Task Workflow_ProgressTracking_IncrementByCustomAmount()
    {
        // Arrange
        var userId = UserId.New();
        var achievementId = AchievementId.New();
        var progress = UserAchievementProgress.Create(userId, achievementId, targetValue: 100);

        // Act
        progress.IncrementProgress(5);
        await Assert.That(progress.CurrentValue).IsEqualTo(5);

        progress.IncrementProgress(10);
        await Assert.That(progress.CurrentValue).IsEqualTo(15);

        progress.IncrementProgress(85);
        await Assert.That(progress.CurrentValue).IsEqualTo(100);

        // Assert
        await Assert.That(progress.IsComplete()).IsTrue();
    }

    [Test]
    public async Task Workflow_ProgressTracking_UpdateProgressDirectly()
    {
        // Arrange
        var userId = UserId.New();
        var achievementId = AchievementId.New();
        var progress = UserAchievementProgress.Create(userId, achievementId, targetValue: 100);

        // Act
        progress.UpdateProgress(50);

        // Assert
        await Assert.That(progress.CurrentValue).IsEqualTo(50);
        await Assert.That(progress.GetProgressPercentage()).IsEqualTo(50.0);
        await Assert.That(progress.LastUpdated).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task Workflow_ProgressTracking_WithProgressData()
    {
        // Arrange
        var userId = UserId.New();
        var achievementId = AchievementId.New();
        var progress = UserAchievementProgress.Create(userId, achievementId, targetValue: 5, progressData: "{\"spaces\":[]}");

        // Act
        progress.IncrementProgress(1, "{\"spaces\":[\"space-1\"]}");

        // Assert
        await Assert.That(progress.CurrentValue).IsEqualTo(1);
        await Assert.That(progress.ProgressData).IsEqualTo("{\"spaces\":[\"space-1\"]}");
    }

    [Test]
    public async Task Workflow_ProgressTracking_ExceedingTargetStillComplete()
    {
        // Arrange
        var progress = UserAchievementProgress.Create(UserId.New(), AchievementId.New(), targetValue: 5);

        // Act
        progress.IncrementProgress(10);

        // Assert
        await Assert.That(progress.CurrentValue).IsEqualTo(10);
        await Assert.That(progress.IsComplete()).IsTrue();
        await Assert.That(progress.GetProgressPercentage()).IsEqualTo(200.0);
    }

    [Test]
    public async Task Workflow_ProgressTracking_NegativeValue_ThrowsException()
    {
        // Arrange
        var progress = UserAchievementProgress.Create(UserId.New(), AchievementId.New(), targetValue: 10);

        // Act & Assert
        await Assert.That(() => progress.UpdateProgress(-1)).Throws<ArgumentException>();
    }

    [Test]
    public async Task Workflow_ProgressTracking_ZeroTargetValue_ThrowsException()
    {
        // Act & Assert
        await Assert.That(() => UserAchievementProgress.Create(UserId.New(), AchievementId.New(), targetValue: 0))
            .Throws<ArgumentException>();
    }

    #endregion

    #region Achievement Lifecycle

    [Test]
    public async Task Workflow_AchievementCreated_Deactivated_Reactivated()
    {
        // Step 1: Create achievement
        var achievement = Achievement.Create(
            "early-bird", "Early Bird", "Register within the first month",
            null, AchievementCategoryEnum.Social, AchievementTierEnum.Gold,
            100, false, AchievementRequirementTypeEnum.Milestone,
            "{\"type\":\"registration_date\"}", 1);

        await Assert.That(achievement.IsActive).IsTrue();

        // Step 2: Deactivate
        achievement.Deactivate();
        await Assert.That(achievement.IsActive).IsFalse();
        await Assert.That(achievement.UpdatedAt).IsNotNull();

        // Step 3: Reactivate
        achievement.Activate();
        await Assert.That(achievement.IsActive).IsTrue();
    }

    [Test]
    public async Task Workflow_UserAchievement_DisplayAndNotification()
    {
        // Step 1: Award achievement
        var userId = UserId.New();
        var achievementId = AchievementId.New();
        var userAchievement = UserAchievement.Create(userId, achievementId);

        await Assert.That(userAchievement.IsDisplayed).IsFalse();
        await Assert.That(userAchievement.NotificationSent).IsFalse();

        // Step 2: User chooses to display it
        userAchievement.UpdateDisplay(true, 1);
        await Assert.That(userAchievement.IsDisplayed).IsTrue();
        await Assert.That(userAchievement.DisplayOrder).IsEqualTo(1);

        // Step 3: Mark notification as sent
        userAchievement.MarkNotificationSent();
        await Assert.That(userAchievement.NotificationSent).IsTrue();
    }

    [Test]
    public async Task Workflow_UserAchievement_Rehydrate_PreservesState()
    {
        // Arrange
        var publicId = UserAchievementId.New();
        var userId = UserId.New();
        var achievementId = AchievementId.New();
        var earnedAt = DateTime.UtcNow.AddDays(-7);

        // Act
        var userAchievement = UserAchievement.Rehydrate(
            publicId, userId, achievementId, earnedAt,
            isDisplayed: true, displayOrder: 3, notificationSent: true);

        // Assert
        await Assert.That(userAchievement.PublicId).IsEqualTo(publicId);
        await Assert.That(userAchievement.UserId).IsEqualTo(userId);
        await Assert.That(userAchievement.AchievementId).IsEqualTo(achievementId);
        await Assert.That(userAchievement.EarnedAt).IsEqualTo(earnedAt);
        await Assert.That(userAchievement.IsDisplayed).IsTrue();
        await Assert.That(userAchievement.DisplayOrder).IsEqualTo(3);
        await Assert.That(userAchievement.NotificationSent).IsTrue();
    }

    #endregion

    #region Achievement Edge Cases

    [Test]
    public async Task Workflow_AchievementCreation_NegativePoints_ThrowsException()
    {
        // Act & Assert
        await Assert.That(() => Achievement.Create(
            "negative", "Negative Points", "Bad achievement",
            null, AchievementCategoryEnum.Content, AchievementTierEnum.Bronze,
            points: -10, false, AchievementRequirementTypeEnum.Count,
            "{\"targetCount\": 1}", 1)).Throws<ArgumentException>();
    }

    [Test]
    public async Task Workflow_AchievementCreation_EmptyName_ThrowsException()
    {
        // Act & Assert
        await Assert.That(() => Achievement.Create(
            "test-slug", "", "Description",
            null, AchievementCategoryEnum.Content, AchievementTierEnum.Bronze,
            10, false, AchievementRequirementTypeEnum.Count,
            "{}", 1)).Throws<ArgumentException>();
    }

    [Test]
    public async Task Workflow_AchievementCreation_ZeroPoints_Succeeds()
    {
        // Act
        var achievement = Achievement.Create(
            "free-achievement", "Free Achievement", "No points required",
            null, AchievementCategoryEnum.Social, AchievementTierEnum.Bronze,
            points: 0, false, AchievementRequirementTypeEnum.Milestone,
            "{\"type\":\"special\"}", 1);

        // Assert
        await Assert.That(achievement.Points).IsEqualTo(0);
    }

    [Test]
    public async Task Workflow_SecretAchievement_IsSecretFlag()
    {
        // Act
        var achievement = Achievement.Create(
            "hidden-gem", "Hidden Gem", "Secret achievement",
            null, AchievementCategoryEnum.Content, AchievementTierEnum.Platinum,
            200, isSecret: true, AchievementRequirementTypeEnum.Milestone,
            "{\"type\":\"hidden\"}", 1);

        // Assert
        await Assert.That(achievement.IsSecret).IsTrue();
    }

    #endregion
}
