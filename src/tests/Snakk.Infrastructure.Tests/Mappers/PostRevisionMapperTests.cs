using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Infrastructure.Mappers;

namespace Snakk.Infrastructure.Tests.Mappers;

public class PostRevisionMapperTests
{
    #region FromPersistence Tests

    [Test]
    public async Task FromPersistence_MapsAllProperties()
    {
        // Arrange
        var postPublicId = Guid.NewGuid().ToString();
        var userPublicId = Guid.NewGuid().ToString();
        var createdAt = new DateTime(2025, 6, 15, 10, 30, 0, DateTimeKind.Utc);

        var entity = new PostRevisionDatabaseEntity
        {
            Id = 1,
            PostId = 42,
            PostPublicId = postPublicId,
            Content = "Original post content before edit",
            EditedByUserId = 7,
            EditedByUserPublicId = userPublicId,
            RevisionNumber = 3,
            CreatedAt = createdAt
        };

        // Act
        var revision = entity.FromPersistence();

        // Assert
        await Assert.That(revision).IsNotNull();
        await Assert.That(revision.PostId.Value).IsEqualTo(postPublicId);
        await Assert.That(revision.Content).IsEqualTo("Original post content before edit");
        await Assert.That(revision.EditedByUserId.Value).IsEqualTo(userPublicId);
        await Assert.That(revision.RevisionNumber).IsEqualTo(3);
        await Assert.That(revision.CreatedAt).IsEqualTo(createdAt);
    }

    [Test]
    public async Task FromPersistence_WithRevisionNumberZero_MapsCorrectly()
    {
        // Arrange
        var entity = new PostRevisionDatabaseEntity
        {
            Id = 1,
            PostId = 1,
            PostPublicId = Guid.NewGuid().ToString(),
            Content = "Initial revision",
            EditedByUserId = 1,
            EditedByUserPublicId = Guid.NewGuid().ToString(),
            RevisionNumber = 0,
            CreatedAt = DateTime.UtcNow
        };

        // Act
        var revision = entity.FromPersistence();

        // Assert
        await Assert.That(revision.RevisionNumber).IsEqualTo(0);
    }

    #endregion

    #region ToPersistence Tests

    [Test]
    public async Task ToPersistence_MapsAllProperties()
    {
        // Arrange
        var postId = PostId.New();
        var userId = UserId.New();
        var createdAt = new DateTime(2025, 6, 15, 10, 30, 0, DateTimeKind.Utc);
        var revision = PostRevision.Rehydrate(postId, "Saved revision content", userId, 2, createdAt);

        // Act
        var entity = revision.ToPersistence();

        // Assert
        await Assert.That(entity).IsNotNull();
        await Assert.That(entity.PostPublicId).IsEqualTo(postId.Value);
        await Assert.That(entity.Content).IsEqualTo("Saved revision content");
        await Assert.That(entity.EditedByUserPublicId).IsEqualTo(userId.Value);
        await Assert.That(entity.RevisionNumber).IsEqualTo(2);
        await Assert.That(entity.CreatedAt).IsEqualTo(createdAt);
    }

    [Test]
    public async Task ToPersistence_WithNewlyCreatedRevision_MapsCorrectly()
    {
        // Arrange
        var postId = PostId.New();
        var userId = UserId.New();
        var revision = PostRevision.Create(postId, "New revision content", userId, 1);

        // Act
        var entity = revision.ToPersistence();

        // Assert
        await Assert.That(entity.PostPublicId).IsEqualTo(postId.Value);
        await Assert.That(entity.Content).IsEqualTo("New revision content");
        await Assert.That(entity.EditedByUserPublicId).IsEqualTo(userId.Value);
        await Assert.That(entity.RevisionNumber).IsEqualTo(1);
        await Assert.That((DateTime.UtcNow - entity.CreatedAt).TotalSeconds).IsLessThan(2);
    }

    #endregion

    #region Round-Trip Tests

    [Test]
    public async Task RoundTrip_PreservesAllData()
    {
        // Arrange
        var postId = PostId.New();
        var userId = UserId.New();
        var createdAt = new DateTime(2025, 3, 20, 14, 0, 0, DateTimeKind.Utc);
        var original = PostRevision.Rehydrate(postId, "Round-trip content", userId, 5, createdAt);

        // Act
        var entity = original.ToPersistence();
        var reconstructed = entity.FromPersistence();

        // Assert
        await Assert.That(reconstructed.PostId).IsEqualTo(original.PostId);
        await Assert.That(reconstructed.Content).IsEqualTo(original.Content);
        await Assert.That(reconstructed.EditedByUserId).IsEqualTo(original.EditedByUserId);
        await Assert.That(reconstructed.RevisionNumber).IsEqualTo(original.RevisionNumber);
        await Assert.That(reconstructed.CreatedAt).IsEqualTo(original.CreatedAt);
    }

    [Test]
    public async Task RoundTrip_WithLargeRevisionNumber_PreservesValue()
    {
        // Arrange
        var postId = PostId.New();
        var userId = UserId.New();
        var original = PostRevision.Rehydrate(postId, "Many edits", userId, 999, DateTime.UtcNow);

        // Act
        var entity = original.ToPersistence();
        var reconstructed = entity.FromPersistence();

        // Assert
        await Assert.That(reconstructed.RevisionNumber).IsEqualTo(999);
        await Assert.That(reconstructed.Content).IsEqualTo("Many edits");
    }

    #endregion
}
