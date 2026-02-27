using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;

namespace Snakk.Domain.Tests.Entities;

public class UserAchievementProgressTests
{
    #region Create Tests

    [Test]
    public async Task Create_WithValidTargetValue_CreatesProgress()
    {
        // Arrange
        var userId = UserId.New();
        var achievementId = AchievementId.New();

        // Act
        var progress = UserAchievementProgress.Create(userId, achievementId, targetValue: 10);

        // Assert
        await Assert.That(progress).IsNotNull();
        await Assert.That(progress.UserId).IsEqualTo(userId);
        await Assert.That(progress.AchievementId).IsEqualTo(achievementId);
        await Assert.That(progress.CurrentValue).IsEqualTo(0);
        await Assert.That(progress.TargetValue).IsEqualTo(10);
        await Assert.That(progress.LastUpdated).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task Create_WithTargetValueZero_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.That(() => UserAchievementProgress.Create(
            UserId.New(), AchievementId.New(), targetValue: 0)).Throws<ArgumentException>();
    }

    [Test]
    public async Task Create_WithNegativeTargetValue_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.That(() => UserAchievementProgress.Create(
            UserId.New(), AchievementId.New(), targetValue: -5)).Throws<ArgumentException>();
    }

    #endregion

    #region UpdateProgress Tests

    [Test]
    public async Task UpdateProgress_WithValidValue_UpdatesCurrentValue()
    {
        // Arrange
        var progress = UserAchievementProgress.Create(UserId.New(), AchievementId.New(), 10);

        // Act
        progress.UpdateProgress(5);

        // Assert
        await Assert.That(progress.CurrentValue).IsEqualTo(5);
        await Assert.That(progress.LastUpdated).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task UpdateProgress_WithNegativeValue_ThrowsArgumentException()
    {
        // Arrange
        var progress = UserAchievementProgress.Create(UserId.New(), AchievementId.New(), 10);

        // Act & Assert
        await Assert.That(() => progress.UpdateProgress(-1)).Throws<ArgumentException>();
    }

    [Test]
    public async Task UpdateProgress_WithProgressData_UpdatesProgressData()
    {
        // Arrange
        var progress = UserAchievementProgress.Create(UserId.New(), AchievementId.New(), 10);

        // Act
        progress.UpdateProgress(3, "{\"details\":\"test\"}");

        // Assert
        await Assert.That(progress.ProgressData).IsEqualTo("{\"details\":\"test\"}");
    }

    #endregion

    #region IncrementProgress Tests

    [Test]
    public async Task IncrementProgress_IncrementsCurrentValue()
    {
        // Arrange
        var progress = UserAchievementProgress.Create(UserId.New(), AchievementId.New(), 10);

        // Act
        progress.IncrementProgress();

        // Assert
        await Assert.That(progress.CurrentValue).IsEqualTo(1);
    }

    [Test]
    public async Task IncrementProgress_MultipleIncrements_AccumulateCorrectly()
    {
        // Arrange
        var progress = UserAchievementProgress.Create(UserId.New(), AchievementId.New(), 10);

        // Act
        progress.IncrementProgress();
        progress.IncrementProgress();
        progress.IncrementProgress();

        // Assert
        await Assert.That(progress.CurrentValue).IsEqualTo(3);
    }

    [Test]
    public async Task IncrementProgress_WithCustomIncrement_IncrementsByAmount()
    {
        // Arrange
        var progress = UserAchievementProgress.Create(UserId.New(), AchievementId.New(), 10);

        // Act
        progress.IncrementProgress(5);

        // Assert
        await Assert.That(progress.CurrentValue).IsEqualTo(5);
    }

    #endregion

    #region IsComplete Tests

    [Test]
    public async Task IsComplete_WhenCurrentValueEqualsTarget_ReturnsTrue()
    {
        // Arrange
        var progress = UserAchievementProgress.Create(UserId.New(), AchievementId.New(), 5);
        progress.UpdateProgress(5);

        // Act
        var isComplete = progress.IsComplete();

        // Assert
        await Assert.That(isComplete).IsTrue();
    }

    [Test]
    public async Task IsComplete_WhenCurrentValueExceedsTarget_ReturnsTrue()
    {
        // Arrange
        var progress = UserAchievementProgress.Create(UserId.New(), AchievementId.New(), 5);
        progress.UpdateProgress(10);

        // Act
        var isComplete = progress.IsComplete();

        // Assert
        await Assert.That(isComplete).IsTrue();
    }

    [Test]
    public async Task IsComplete_WhenCurrentValueBelowTarget_ReturnsFalse()
    {
        // Arrange
        var progress = UserAchievementProgress.Create(UserId.New(), AchievementId.New(), 10);
        progress.UpdateProgress(3);

        // Act
        var isComplete = progress.IsComplete();

        // Assert
        await Assert.That(isComplete).IsFalse();
    }

    #endregion

    #region GetProgressPercentage Tests

    [Test]
    public async Task GetProgressPercentage_CalculatesCorrectly()
    {
        // Arrange
        var progress = UserAchievementProgress.Create(UserId.New(), AchievementId.New(), 10);
        progress.UpdateProgress(5);

        // Act
        var percentage = progress.GetProgressPercentage();

        // Assert
        await Assert.That(percentage).IsEqualTo(50.0);
    }

    [Test]
    public async Task GetProgressPercentage_AtZeroProgress_ReturnsZero()
    {
        // Arrange
        var progress = UserAchievementProgress.Create(UserId.New(), AchievementId.New(), 10);

        // Act
        var percentage = progress.GetProgressPercentage();

        // Assert
        await Assert.That(percentage).IsEqualTo(0.0);
    }

    [Test]
    public async Task GetProgressPercentage_AtComplete_Returns100()
    {
        // Arrange
        var progress = UserAchievementProgress.Create(UserId.New(), AchievementId.New(), 10);
        progress.UpdateProgress(10);

        // Act
        var percentage = progress.GetProgressPercentage();

        // Assert
        await Assert.That(percentage).IsEqualTo(100.0);
    }

    [Test]
    public async Task GetProgressPercentage_WithZeroTargetValue_ReturnsZero()
    {
        // Arrange - use Rehydrate to set up a zero target value (Create doesn't allow it)
        var progress = UserAchievementProgress.Rehydrate(
            UserId.New(), AchievementId.New(), 5, 0, null, DateTime.UtcNow);

        // Act
        var percentage = progress.GetProgressPercentage();

        // Assert
        await Assert.That(percentage).IsEqualTo(0.0);
    }

    #endregion

    #region Rehydrate Tests

    [Test]
    public async Task Rehydrate_RestoresAllProperties()
    {
        // Arrange
        var userId = UserId.New();
        var achievementId = AchievementId.New();
        var lastUpdated = DateTime.UtcNow.AddDays(-1);

        // Act
        var progress = UserAchievementProgress.Rehydrate(
            userId, achievementId, 7, 10, "{\"data\":true}", lastUpdated);

        // Assert
        await Assert.That(progress.UserId).IsEqualTo(userId);
        await Assert.That(progress.AchievementId).IsEqualTo(achievementId);
        await Assert.That(progress.CurrentValue).IsEqualTo(7);
        await Assert.That(progress.TargetValue).IsEqualTo(10);
        await Assert.That(progress.ProgressData).IsEqualTo("{\"data\":true}");
        await Assert.That(progress.LastUpdated).IsEqualTo(lastUpdated);
    }

    #endregion
}
