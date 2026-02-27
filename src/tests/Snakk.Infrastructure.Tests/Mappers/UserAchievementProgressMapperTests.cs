using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Infrastructure.Mappers;

namespace Snakk.Infrastructure.Tests.Mappers;

public class UserAchievementProgressMapperTests
{
    #region FromPersistence Tests

    [Test]
    public async Task FromPersistence_MapsAllProperties()
    {
        // Arrange
        var userPublicId = Guid.NewGuid().ToString();
        var achievementPublicId = Guid.NewGuid().ToString();
        var lastUpdated = new DateTime(2025, 8, 10, 14, 30, 0, DateTimeKind.Utc);

        var entity = new UserAchievementProgressDatabaseEntity
        {
            Id = 1,
            CurrentValue = 7,
            TargetValue = 10,
            ProgressData = "{\"lastAction\":\"post_created\"}",
            LastUpdated = lastUpdated,
            UserId = 42,
            User = new UserDatabaseEntity
            {
                PublicId = userPublicId,
                DisplayName = "TestUser",
                Email = "test@example.com",
                CreatedAt = DateTime.UtcNow,
                LastModifiedAt = DateTime.UtcNow
            },
            AchievementId = 5,
            Achievement = new AchievementDatabaseEntity
            {
                PublicId = achievementPublicId,
                Slug = "first-10-posts",
                Name = "First 10 Posts",
                Description = "Create 10 posts",
                CreatedAt = DateTime.UtcNow,
                RequirementConfig = "{}"
            }
        };

        // Act
        var progress = entity.FromPersistence();

        // Assert
        await Assert.That(progress).IsNotNull();
        await Assert.That(progress.UserId.Value).IsEqualTo(userPublicId);
        await Assert.That(progress.AchievementId.Value).IsEqualTo(achievementPublicId);
        await Assert.That(progress.CurrentValue).IsEqualTo(7);
        await Assert.That(progress.TargetValue).IsEqualTo(10);
        await Assert.That(progress.ProgressData).IsEqualTo("{\"lastAction\":\"post_created\"}");
        await Assert.That(progress.LastUpdated).IsEqualTo(lastUpdated);
    }

    [Test]
    public async Task FromPersistence_WithNullProgressData_MapsNull()
    {
        // Arrange
        var entity = new UserAchievementProgressDatabaseEntity
        {
            Id = 2,
            CurrentValue = 3,
            TargetValue = 5,
            ProgressData = null,
            LastUpdated = DateTime.UtcNow,
            UserId = 1,
            User = new UserDatabaseEntity
            {
                PublicId = Guid.NewGuid().ToString(),
                DisplayName = "TestUser",
                Email = "test@example.com",
                CreatedAt = DateTime.UtcNow,
                LastModifiedAt = DateTime.UtcNow
            },
            AchievementId = 1,
            Achievement = new AchievementDatabaseEntity
            {
                PublicId = Guid.NewGuid().ToString(),
                Slug = "test-achievement",
                Name = "Test",
                Description = "Test achievement",
                CreatedAt = DateTime.UtcNow,
                RequirementConfig = "{}"
            }
        };

        // Act
        var progress = entity.FromPersistence();

        // Assert
        await Assert.That(progress.ProgressData).IsNull();
    }

    [Test]
    public async Task FromPersistence_WithCompletedProgress_MapsCorrectly()
    {
        // Arrange
        var entity = new UserAchievementProgressDatabaseEntity
        {
            Id = 3,
            CurrentValue = 10,
            TargetValue = 10,
            ProgressData = null,
            LastUpdated = DateTime.UtcNow,
            UserId = 1,
            User = new UserDatabaseEntity
            {
                PublicId = Guid.NewGuid().ToString(),
                DisplayName = "TestUser",
                Email = "test@example.com",
                CreatedAt = DateTime.UtcNow,
                LastModifiedAt = DateTime.UtcNow
            },
            AchievementId = 1,
            Achievement = new AchievementDatabaseEntity
            {
                PublicId = Guid.NewGuid().ToString(),
                Slug = "completed-achievement",
                Name = "Completed",
                Description = "Already completed",
                CreatedAt = DateTime.UtcNow,
                RequirementConfig = "{}"
            }
        };

        // Act
        var progress = entity.FromPersistence();

        // Assert
        await Assert.That(progress.CurrentValue).IsEqualTo(10);
        await Assert.That(progress.TargetValue).IsEqualTo(10);
        await Assert.That(progress.IsComplete()).IsTrue();
    }

    #endregion

    #region ToPersistence Tests

    [Test]
    public async Task ToPersistence_MapsValueProperties()
    {
        // Arrange
        var userId = UserId.New();
        var achievementId = AchievementId.New();
        var lastUpdated = new DateTime(2025, 8, 10, 14, 30, 0, DateTimeKind.Utc);
        var progress = UserAchievementProgress.Rehydrate(userId, achievementId, 5, 10, "{\"key\":\"value\"}", lastUpdated);

        // Act
        var entity = progress.ToPersistence();

        // Assert
        await Assert.That(entity).IsNotNull();
        await Assert.That(entity.CurrentValue).IsEqualTo(5);
        await Assert.That(entity.TargetValue).IsEqualTo(10);
        await Assert.That(entity.ProgressData).IsEqualTo("{\"key\":\"value\"}");
        await Assert.That(entity.LastUpdated).IsEqualTo(lastUpdated);
    }

    [Test]
    public async Task ToPersistence_WithNullProgressData_MapsNull()
    {
        // Arrange
        var progress = UserAchievementProgress.Rehydrate(
            UserId.New(), AchievementId.New(), 2, 5, null, DateTime.UtcNow);

        // Act
        var entity = progress.ToPersistence();

        // Assert
        await Assert.That(entity.ProgressData).IsNull();
    }

    [Test]
    public async Task ToPersistence_DoesNotSetNavigationOrForeignKeyProperties()
    {
        // Arrange - UserId and AchievementId are set by repository adapter, not mapper
        var progress = UserAchievementProgress.Rehydrate(
            UserId.New(), AchievementId.New(), 1, 10, null, DateTime.UtcNow);

        // Act
        var entity = progress.ToPersistence();

        // Assert - Foreign keys should be default (0) since mapper doesn't set them
        await Assert.That(entity.UserId).IsEqualTo(0);
        await Assert.That(entity.AchievementId).IsEqualTo(0);
    }

    #endregion

    #region Round-Trip Tests

    [Test]
    public async Task RoundTrip_PreservesValueProperties()
    {
        // Arrange
        var userId = UserId.New();
        var achievementId = AchievementId.New();
        var lastUpdated = new DateTime(2025, 9, 1, 8, 0, 0, DateTimeKind.Utc);
        var original = UserAchievementProgress.Rehydrate(userId, achievementId, 4, 20, "{\"streak\":3}", lastUpdated);

        // Act
        var entity = original.ToPersistence();
        // Simulate repository adapter setting navigation properties
        entity.User = new UserDatabaseEntity
        {
            PublicId = userId.Value,
            DisplayName = "TestUser",
            Email = "test@example.com",
            CreatedAt = DateTime.UtcNow,
            LastModifiedAt = DateTime.UtcNow
        };
        entity.Achievement = new AchievementDatabaseEntity
        {
            PublicId = achievementId.Value,
            Slug = "test-achievement",
            Name = "Test Achievement",
            Description = "Test",
            CreatedAt = DateTime.UtcNow,
            RequirementConfig = "{}"
        };
        var reconstructed = entity.FromPersistence();

        // Assert
        await Assert.That(reconstructed.UserId).IsEqualTo(original.UserId);
        await Assert.That(reconstructed.AchievementId).IsEqualTo(original.AchievementId);
        await Assert.That(reconstructed.CurrentValue).IsEqualTo(original.CurrentValue);
        await Assert.That(reconstructed.TargetValue).IsEqualTo(original.TargetValue);
        await Assert.That(reconstructed.ProgressData).IsEqualTo(original.ProgressData);
        await Assert.That(reconstructed.LastUpdated).IsEqualTo(original.LastUpdated);
    }

    [Test]
    public async Task RoundTrip_WithNullProgressData_PreservesNull()
    {
        // Arrange
        var userId = UserId.New();
        var achievementId = AchievementId.New();
        var original = UserAchievementProgress.Rehydrate(userId, achievementId, 0, 5, null, DateTime.UtcNow);

        // Act
        var entity = original.ToPersistence();
        entity.User = new UserDatabaseEntity
        {
            PublicId = userId.Value,
            DisplayName = "TestUser",
            Email = "test@example.com",
            CreatedAt = DateTime.UtcNow,
            LastModifiedAt = DateTime.UtcNow
        };
        entity.Achievement = new AchievementDatabaseEntity
        {
            PublicId = achievementId.Value,
            Slug = "test",
            Name = "Test",
            Description = "Test",
            CreatedAt = DateTime.UtcNow,
            RequirementConfig = "{}"
        };
        var reconstructed = entity.FromPersistence();

        // Assert
        await Assert.That(reconstructed.ProgressData).IsNull();
        await Assert.That(reconstructed.CurrentValue).IsEqualTo(0);
    }

    #endregion
}
