using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Infrastructure.Mappers;

namespace Snakk.Infrastructure.Tests.Mappers;

public class MentionMapperTests
{
    #region FromPersistence Tests

    [Test]
    public async Task FromPersistence_MapsAllProperties()
    {
        // Arrange
        var mentionPublicId = Guid.NewGuid().ToString();
        var postPublicId = Guid.NewGuid().ToString();
        var userPublicId = Guid.NewGuid().ToString();
        var createdAt = new DateTime(2025, 8, 5, 12, 0, 0, DateTimeKind.Utc);

        var entity = new MentionDatabaseEntity
        {
            Id = 1,
            PublicId = mentionPublicId,
            PostId = 42,
            MentionedUserId = 7,
            CreatedAt = createdAt,
            Post = new PostDatabaseEntity { PublicId = postPublicId, Content = "Test post content", CreatedAt = DateTime.UtcNow },
            MentionedUser = new UserDatabaseEntity
            {
                PublicId = userPublicId,
                DisplayName = "MentionedUser",
                CreatedAt = DateTime.UtcNow
            }
        };

        // Act
        var mention = entity.FromPersistence();

        // Assert
        await Assert.That(mention).IsNotNull();
        await Assert.That(mention.PublicId.Value).IsEqualTo(mentionPublicId);
        await Assert.That(mention.PostId.Value).IsEqualTo(postPublicId);
        await Assert.That(mention.MentionedUserId.Value).IsEqualTo(userPublicId);
        await Assert.That(mention.CreatedAt).IsEqualTo(createdAt);
    }

    [Test]
    public async Task FromPersistence_UsesNavigationPropertyPublicIds()
    {
        // Arrange - the mapper reads PublicId from navigation properties, NOT from FK integers
        var postNavPublicId = "post_nav_public_id";
        var userNavPublicId = "user_nav_public_id";

        var entity = new MentionDatabaseEntity
        {
            Id = 2,
            PublicId = Guid.NewGuid().ToString(),
            PostId = 999,  // FK integer - should NOT be used by mapper
            MentionedUserId = 888,  // FK integer - should NOT be used by mapper
            CreatedAt = DateTime.UtcNow,
            Post = new PostDatabaseEntity { PublicId = postNavPublicId, Content = "Nav post content", CreatedAt = DateTime.UtcNow },
            MentionedUser = new UserDatabaseEntity
            {
                PublicId = userNavPublicId,
                DisplayName = "NavUser",
                CreatedAt = DateTime.UtcNow
            }
        };

        // Act
        var mention = entity.FromPersistence();

        // Assert - verify IDs come from navigation properties
        await Assert.That(mention.PostId.Value).IsEqualTo(postNavPublicId);
        await Assert.That(mention.MentionedUserId.Value).IsEqualTo(userNavPublicId);
    }

    #endregion

    #region ToPersistence Tests

    [Test]
    public async Task ToPersistence_MapsPublicIdAndCreatedAt()
    {
        // Arrange
        var mentionId = MentionId.New();
        var postId = PostId.New();
        var userId = UserId.New();
        var createdAt = new DateTime(2025, 9, 1, 8, 30, 0, DateTimeKind.Utc);

        var mention = Mention.Rehydrate(mentionId, postId, userId, createdAt);

        // Act
        var entity = mention.ToPersistence();

        // Assert
        await Assert.That(entity).IsNotNull();
        await Assert.That(entity.PublicId).IsEqualTo(mentionId.Value);
        await Assert.That(entity.CreatedAt).IsEqualTo(createdAt);
    }

    [Test]
    public async Task ToPersistence_DoesNotSetForeignKeys()
    {
        // Arrange - ToPersistence should NOT set PostId or MentionedUserId FK integers
        // Those are set by the repository adapter
        var mention = Mention.Rehydrate(
            MentionId.New(),
            PostId.New(),
            UserId.New(),
            DateTime.UtcNow);

        // Act
        var entity = mention.ToPersistence();

        // Assert - FK integers should be default (0) since mapper doesn't set them
        await Assert.That(entity.PostId).IsEqualTo(0);
        await Assert.That(entity.MentionedUserId).IsEqualTo(0);
    }

    #endregion

    #region Round-Trip Tests

    [Test]
    public async Task RoundTrip_PublicIdAndCreatedAt_Preserved()
    {
        // Note: Full round-trip is not possible because ToPersistence doesn't set
        // navigation properties (Post, MentionedUser). We can only verify that
        // PublicId and CreatedAt survive the round-trip by simulating the repository
        // adapter setting navigation properties on the persisted entity.
        var mentionId = MentionId.New();
        var postId = PostId.New();
        var userId = UserId.New();
        var createdAt = new DateTime(2025, 7, 20, 16, 0, 0, DateTimeKind.Utc);

        var original = Mention.Rehydrate(mentionId, postId, userId, createdAt);

        // Act
        var entity = original.ToPersistence();

        // Simulate repository adapter setting navigation properties
        entity.Post = new PostDatabaseEntity { PublicId = postId.Value, Content = "Round-trip post content", CreatedAt = DateTime.UtcNow };
        entity.MentionedUser = new UserDatabaseEntity
        {
            PublicId = userId.Value,
            DisplayName = "RoundTripUser",
            CreatedAt = DateTime.UtcNow
        };

        var reconstructed = entity.FromPersistence();

        // Assert
        await Assert.That(reconstructed.PublicId).IsEqualTo(original.PublicId);
        await Assert.That(reconstructed.PostId).IsEqualTo(original.PostId);
        await Assert.That(reconstructed.MentionedUserId).IsEqualTo(original.MentionedUserId);
        await Assert.That(reconstructed.CreatedAt).IsEqualTo(original.CreatedAt);
    }

    #endregion
}
