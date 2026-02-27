using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Infrastructure.Mappers;

namespace Snakk.Infrastructure.Tests.Mappers;

public class UserAchievementMapperTests
{
    #region FromPersistence Tests

    [Test]
    public async Task FromPersistence_MapsAllProperties()
    {
        // Arrange
        var publicId = Guid.NewGuid().ToString();
        var userPublicId = Guid.NewGuid().ToString();
        var achievementPublicId = Guid.NewGuid().ToString();
        var earnedAt = new DateTime(2025, 8, 20, 14, 0, 0, DateTimeKind.Utc);

        var entity = new UserAchievementDatabaseEntity
        {
            Id = 1,
            PublicId = publicId,
            UserId = 42,
            AchievementId = 5,
            EarnedAt = earnedAt,
            IsDisplayed = true,
            DisplayOrder = 3,
            NotificationSent = false,
            User = new UserDatabaseEntity
            {
                PublicId = userPublicId,
                DisplayName = "TestUser",
                CreatedAt = DateTime.UtcNow
            },
            Achievement = new AchievementDatabaseEntity
            {
                PublicId = achievementPublicId,
                Name = "First Post",
                Slug = "first-post",
                Description = "Create your first post",
                CategoryId = 1,
                TierLevel = 1,
                Points = 10,
                IsSecret = false,
                IsActive = true,
                RequirementTypeId = 1,
                RequirementConfig = "{}",
                DisplayOrder = 0,
                CreatedAt = DateTime.UtcNow
            }
        };

        // Act
        var userAchievement = entity.FromPersistence();

        // Assert
        await Assert.That(userAchievement).IsNotNull();
        await Assert.That(userAchievement.PublicId.Value).IsEqualTo(publicId);
        await Assert.That(userAchievement.UserId.Value).IsEqualTo(userPublicId);
        await Assert.That(userAchievement.AchievementId.Value).IsEqualTo(achievementPublicId);
        await Assert.That(userAchievement.EarnedAt).IsEqualTo(earnedAt);
        await Assert.That(userAchievement.IsDisplayed).IsTrue();
        await Assert.That(userAchievement.DisplayOrder).IsEqualTo(3);
        await Assert.That(userAchievement.NotificationSent).IsFalse();
    }

    [Test]
    public async Task FromPersistence_UsesNavigationPropertyPublicIds()
    {
        // Arrange - the mapper reads PublicId from navigation properties, NOT from FK integers
        var userNavPublicId = "user_nav_public_id";
        var achievementNavPublicId = "ach_nav_public_id";

        var entity = new UserAchievementDatabaseEntity
        {
            Id = 2,
            PublicId = Guid.NewGuid().ToString(),
            UserId = 999,  // FK integer - should NOT be used by mapper
            AchievementId = 888,  // FK integer - should NOT be used by mapper
            EarnedAt = DateTime.UtcNow,
            IsDisplayed = false,
            DisplayOrder = 0,
            NotificationSent = false,
            User = new UserDatabaseEntity
            {
                PublicId = userNavPublicId,
                DisplayName = "NavUser",
                CreatedAt = DateTime.UtcNow
            },
            Achievement = new AchievementDatabaseEntity
            {
                PublicId = achievementNavPublicId,
                Name = "Nav Achievement",
                Slug = "nav-achievement",
                Description = "Test",
                CategoryId = 1,
                TierLevel = 1,
                Points = 5,
                IsSecret = false,
                IsActive = true,
                RequirementTypeId = 1,
                RequirementConfig = "{}",
                DisplayOrder = 0,
                CreatedAt = DateTime.UtcNow
            }
        };

        // Act
        var userAchievement = entity.FromPersistence();

        // Assert - verify IDs come from navigation properties
        await Assert.That(userAchievement.UserId.Value).IsEqualTo(userNavPublicId);
        await Assert.That(userAchievement.AchievementId.Value).IsEqualTo(achievementNavPublicId);
    }

    [Test]
    public async Task FromPersistence_WithDisplayedAchievement_MapsCorrectly()
    {
        // Arrange
        var entity = new UserAchievementDatabaseEntity
        {
            Id = 3,
            PublicId = Guid.NewGuid().ToString(),
            UserId = 1,
            AchievementId = 1,
            EarnedAt = DateTime.UtcNow,
            IsDisplayed = true,
            DisplayOrder = 1,
            NotificationSent = true,
            User = new UserDatabaseEntity
            {
                PublicId = Guid.NewGuid().ToString(),
                DisplayName = "DisplayedUser",
                CreatedAt = DateTime.UtcNow
            },
            Achievement = new AchievementDatabaseEntity
            {
                PublicId = Guid.NewGuid().ToString(),
                Name = "Displayed Achievement",
                Slug = "displayed",
                Description = "Displayed on profile",
                CategoryId = 1,
                TierLevel = 1,
                Points = 10,
                IsSecret = false,
                IsActive = true,
                RequirementTypeId = 1,
                RequirementConfig = "{}",
                DisplayOrder = 0,
                CreatedAt = DateTime.UtcNow
            }
        };

        // Act
        var userAchievement = entity.FromPersistence();

        // Assert
        await Assert.That(userAchievement.IsDisplayed).IsTrue();
        await Assert.That(userAchievement.DisplayOrder).IsEqualTo(1);
    }

    [Test]
    public async Task FromPersistence_WithNotificationSent_MapsCorrectly()
    {
        // Arrange
        var entity = new UserAchievementDatabaseEntity
        {
            Id = 4,
            PublicId = Guid.NewGuid().ToString(),
            UserId = 1,
            AchievementId = 1,
            EarnedAt = DateTime.UtcNow,
            IsDisplayed = false,
            DisplayOrder = 0,
            NotificationSent = true,
            User = new UserDatabaseEntity
            {
                PublicId = Guid.NewGuid().ToString(),
                DisplayName = "NotifiedUser",
                CreatedAt = DateTime.UtcNow
            },
            Achievement = new AchievementDatabaseEntity
            {
                PublicId = Guid.NewGuid().ToString(),
                Name = "Notified Achievement",
                Slug = "notified",
                Description = "Notification was sent",
                CategoryId = 1,
                TierLevel = 1,
                Points = 5,
                IsSecret = false,
                IsActive = true,
                RequirementTypeId = 1,
                RequirementConfig = "{}",
                DisplayOrder = 0,
                CreatedAt = DateTime.UtcNow
            }
        };

        // Act
        var userAchievement = entity.FromPersistence();

        // Assert
        await Assert.That(userAchievement.NotificationSent).IsTrue();
    }

    #endregion

    #region ToPersistence Tests

    [Test]
    public async Task ToPersistence_MapsAllProperties()
    {
        // Arrange
        var userAchievementId = UserAchievementId.New();
        var userId = UserId.New();
        var achievementId = AchievementId.New();
        var earnedAt = new DateTime(2025, 9, 15, 10, 0, 0, DateTimeKind.Utc);

        var userAchievement = UserAchievement.Rehydrate(
            userAchievementId,
            userId,
            achievementId,
            earnedAt,
            true,
            5,
            true);

        // Act
        var entity = userAchievement.ToPersistence();

        // Assert
        await Assert.That(entity).IsNotNull();
        await Assert.That(entity.PublicId).IsEqualTo(userAchievementId.Value);
        await Assert.That(entity.EarnedAt).IsEqualTo(earnedAt);
        await Assert.That(entity.IsDisplayed).IsTrue();
        await Assert.That(entity.DisplayOrder).IsEqualTo(5);
        await Assert.That(entity.NotificationSent).IsTrue();
    }

    [Test]
    public async Task ToPersistence_DoesNotSetForeignKeys()
    {
        // Arrange - ToPersistence should NOT set UserId or AchievementId FK integers
        // Those are set by the repository adapter
        var userAchievement = UserAchievement.Rehydrate(
            UserAchievementId.New(),
            UserId.New(),
            AchievementId.New(),
            DateTime.UtcNow,
            false,
            0,
            false);

        // Act
        var entity = userAchievement.ToPersistence();

        // Assert - FK integers should be default (0) since mapper doesn't set them
        await Assert.That(entity.UserId).IsEqualTo(0);
        await Assert.That(entity.AchievementId).IsEqualTo(0);
    }

    #endregion

    #region Round-Trip Tests

    [Test]
    public async Task RoundTrip_PublicIdAndScalarProperties_Preserved()
    {
        // Note: Full round-trip requires simulating the repository adapter
        // setting navigation properties, since ToPersistence doesn't set them.
        var userAchievementId = UserAchievementId.New();
        var userId = UserId.New();
        var achievementId = AchievementId.New();
        var earnedAt = new DateTime(2025, 7, 4, 18, 30, 0, DateTimeKind.Utc);

        var original = UserAchievement.Rehydrate(
            userAchievementId,
            userId,
            achievementId,
            earnedAt,
            true,
            7,
            true);

        // Act
        var entity = original.ToPersistence();

        // Simulate repository adapter setting navigation properties
        entity.User = new UserDatabaseEntity
        {
            PublicId = userId.Value,
            DisplayName = "RoundTripUser",
            CreatedAt = DateTime.UtcNow
        };
        entity.Achievement = new AchievementDatabaseEntity
        {
            PublicId = achievementId.Value,
            Name = "Round Trip Achievement",
            Slug = "round-trip",
            Description = "Tests round-trip mapping",
            CategoryId = 1,
            TierLevel = 1,
            Points = 10,
            IsSecret = false,
            IsActive = true,
            RequirementTypeId = 1,
            RequirementConfig = "{}",
            DisplayOrder = 0,
            CreatedAt = DateTime.UtcNow
        };

        var reconstructed = entity.FromPersistence();

        // Assert
        await Assert.That(reconstructed.PublicId).IsEqualTo(original.PublicId);
        await Assert.That(reconstructed.UserId).IsEqualTo(original.UserId);
        await Assert.That(reconstructed.AchievementId).IsEqualTo(original.AchievementId);
        await Assert.That(reconstructed.EarnedAt).IsEqualTo(original.EarnedAt);
        await Assert.That(reconstructed.IsDisplayed).IsEqualTo(original.IsDisplayed);
        await Assert.That(reconstructed.DisplayOrder).IsEqualTo(original.DisplayOrder);
        await Assert.That(reconstructed.NotificationSent).IsEqualTo(original.NotificationSent);
    }

    #endregion
}
