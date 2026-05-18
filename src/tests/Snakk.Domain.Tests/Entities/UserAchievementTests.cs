using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;

namespace Snakk.Domain.Tests.Entities;

public class UserAchievementTests
{
    #region Create Tests

    [Test]
    public async Task Create_WithValidParameters_CreatesUserAchievement()
    {
        // Arrange
        var userId = UserId.New();
        var achievementId = AchievementId.New();

        // Act
        var ua = UserAchievement.Create(userId, achievementId);

        // Assert
        await Assert.That(ua).IsNotNull();
        await Assert.That(ua.PublicId).IsNotNull();
        await Assert.That((string)ua.UserId).IsEqualTo((string)userId);
        await Assert.That((string)ua.AchievementId).IsEqualTo((string)achievementId);
        await Assert.That(ua.EarnedAt).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
        await Assert.That(ua.IsDisplayed).IsFalse();
        await Assert.That(ua.DisplayOrder).IsEqualTo(0);
        await Assert.That(ua.NotificationSent).IsFalse();
    }

    [Test]
    public async Task Create_EachCall_ProducesUniquePublicId()
    {
        // Arrange
        var userId = UserId.New();
        var achievementId = AchievementId.New();

        // Act
        var ua1 = UserAchievement.Create(userId, achievementId);
        var ua2 = UserAchievement.Create(userId, achievementId);

        // Assert
        await Assert.That((string)ua1.PublicId).IsNotEqualTo((string)ua2.PublicId);
    }

    #endregion

    #region Rehydrate Tests

    [Test]
    public async Task Rehydrate_WithAllParameters_CreatesUserAchievementWithExactState()
    {
        // Arrange
        var uaId = UserAchievementId.New();
        var userId = UserId.New();
        var achievementId = AchievementId.New();
        var earnedAt = DateTime.UtcNow.AddDays(-5);

        // Act
        var ua = UserAchievement.Rehydrate(uaId, userId, achievementId, earnedAt, isDisplayed: true, displayOrder: 3, notificationSent: true);

        // Assert
        await Assert.That((string)ua.PublicId).IsEqualTo((string)uaId);
        await Assert.That((string)ua.UserId).IsEqualTo((string)userId);
        await Assert.That((string)ua.AchievementId).IsEqualTo((string)achievementId);
        await Assert.That(ua.EarnedAt).IsEqualTo(earnedAt);
        await Assert.That(ua.IsDisplayed).IsTrue();
        await Assert.That(ua.DisplayOrder).IsEqualTo(3);
        await Assert.That(ua.NotificationSent).IsTrue();
    }

    #endregion

    #region UpdateDisplay Tests

    [Test]
    public async Task UpdateDisplay_WithIsDisplayedTrue_SetsIsDisplayedAndDisplayOrder()
    {
        // Arrange
        var ua = UserAchievement.Create(UserId.New(), AchievementId.New());
        await Assert.That(ua.IsDisplayed).IsFalse();
        await Assert.That(ua.DisplayOrder).IsEqualTo(0);

        // Act
        ua.UpdateDisplay(isDisplayed: true, displayOrder: 7);

        // Assert
        await Assert.That(ua.IsDisplayed).IsTrue();
        await Assert.That(ua.DisplayOrder).IsEqualTo(7);
    }

    [Test]
    public async Task UpdateDisplay_WithIsDisplayedFalse_ClearsDisplay()
    {
        // Arrange
        var ua = UserAchievement.Rehydrate(
            UserAchievementId.New(), UserId.New(), AchievementId.New(),
            DateTime.UtcNow, isDisplayed: true, displayOrder: 2, notificationSent: false);

        // Act
        ua.UpdateDisplay(isDisplayed: false, displayOrder: 0);

        // Assert
        await Assert.That(ua.IsDisplayed).IsFalse();
        await Assert.That(ua.DisplayOrder).IsEqualTo(0);
    }

    #endregion

    #region MarkNotificationSent Tests

    [Test]
    public async Task MarkNotificationSent_SetsNotificationSentToTrue()
    {
        // Arrange
        var ua = UserAchievement.Create(UserId.New(), AchievementId.New());
        await Assert.That(ua.NotificationSent).IsFalse();

        // Act
        ua.MarkNotificationSent();

        // Assert
        await Assert.That(ua.NotificationSent).IsTrue();
    }

    [Test]
    public async Task MarkNotificationSent_CalledTwice_RemainsTrue()
    {
        // Arrange
        var ua = UserAchievement.Create(UserId.New(), AchievementId.New());

        // Act
        ua.MarkNotificationSent();
        ua.MarkNotificationSent();

        // Assert
        await Assert.That(ua.NotificationSent).IsTrue();
    }

    #endregion
}
