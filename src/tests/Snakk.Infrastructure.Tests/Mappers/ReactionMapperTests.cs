using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Infrastructure.Mappers;
using Snakk.Shared.Enums;

namespace Snakk.Infrastructure.Tests.Mappers;

public class ReactionMapperTests
{
    #region ToPersistence Tests

    [Test]
    public async Task ToPersistence_WithThumbsUpReaction_MapsToCorrectTypeId()
    {
        // Arrange
        var reaction = Reaction.Create(PostId.New(), UserId.New(), ReactionType.ThumbsUp);

        // Act
        var entity = reaction.ToPersistence();

        // Assert
        await Assert.That(entity).IsNotNull();
        await Assert.That(entity.PublicId).IsEqualTo(reaction.PublicId);
        await Assert.That(entity.TypeId).IsEqualTo((int)ReactionTypeEnum.ThumbsUp);
        await Assert.That((DateTime.UtcNow - entity.CreatedAt).TotalSeconds).IsLessThan(1);
    }

    [Test]
    public async Task ToPersistence_WithHeartReaction_MapsToCorrectTypeId()
    {
        // Arrange
        var reaction = Reaction.Create(PostId.New(), UserId.New(), ReactionType.Heart);

        // Act
        var entity = reaction.ToPersistence();

        // Assert
        await Assert.That(entity.TypeId).IsEqualTo((int)ReactionTypeEnum.Heart);
    }

    [Test]
    public async Task ToPersistence_WithEyesReaction_MapsToCorrectTypeId()
    {
        // Arrange
        var reaction = Reaction.Create(PostId.New(), UserId.New(), ReactionType.Eyes);

        // Act
        var entity = reaction.ToPersistence();

        // Assert
        await Assert.That(entity.TypeId).IsEqualTo((int)ReactionTypeEnum.Eyes);
    }

    [Test]
    public async Task ToPersistence_MapsAllProperties()
    {
        // Arrange
        var postId = PostId.New();
        var userId = UserId.New();
        var reaction = Reaction.Create(postId, userId, ReactionType.Heart);

        // Act
        var entity = reaction.ToPersistence();

        // Assert
        await Assert.That(entity.PublicId).IsEqualTo(reaction.PublicId);
        await Assert.That(entity.TypeId).IsEqualTo((int)ReactionTypeEnum.Heart);
        await Assert.That(entity.CreatedAt).IsEqualTo(reaction.CreatedAt);
    }

    #endregion

    #region FromPersistence Tests

    [Test]
    public async Task FromPersistence_WithThumbsUpTypeId_ReconstructsThumbsUpReaction()
    {
        // Arrange
        var postPublicId = Guid.NewGuid().ToString();
        var userPublicId = Guid.NewGuid().ToString();
        var entity = new ReactionDatabaseEntity
        {
            PublicId = Guid.NewGuid().ToString(),
            TypeId = (int)ReactionTypeEnum.ThumbsUp,
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            Post = new PostDatabaseEntity
            {
                PublicId = postPublicId,
                Content = "test",
                CreatedAt = DateTime.UtcNow,
                LastModifiedAt = DateTime.UtcNow
            },
            User = new UserDatabaseEntity
            {
                PublicId = userPublicId,
                DisplayName = "test",
                Email = "test@example.com",
                CreatedAt = DateTime.UtcNow,
                LastModifiedAt = DateTime.UtcNow
            }
        };

        // Act
        var reaction = entity.FromPersistence();

        // Assert
        await Assert.That(reaction).IsNotNull();
        await Assert.That(reaction.PublicId.Value).IsEqualTo(entity.PublicId);
        await Assert.That(reaction.Type).IsEqualTo(ReactionType.ThumbsUp);
        await Assert.That(reaction.PostId.Value).IsEqualTo(postPublicId);
        await Assert.That(reaction.UserId.Value).IsEqualTo(userPublicId);
        await Assert.That(reaction.CreatedAt).IsEqualTo(entity.CreatedAt);
    }

    [Test]
    public async Task FromPersistence_WithHeartTypeId_ReconstructsHeartReaction()
    {
        // Arrange
        var entity = new ReactionDatabaseEntity
        {
            PublicId = Guid.NewGuid().ToString(),
            TypeId = (int)ReactionTypeEnum.Heart,
            CreatedAt = DateTime.UtcNow,
            Post = new PostDatabaseEntity
            {
                PublicId = Guid.NewGuid().ToString(),
                Content = "test",
                CreatedAt = DateTime.UtcNow,
                LastModifiedAt = DateTime.UtcNow
            },
            User = new UserDatabaseEntity
            {
                PublicId = Guid.NewGuid().ToString(),
                DisplayName = "test",
                Email = "test@example.com",
                CreatedAt = DateTime.UtcNow,
                LastModifiedAt = DateTime.UtcNow
            }
        };

        // Act
        var reaction = entity.FromPersistence();

        // Assert
        await Assert.That(reaction.Type).IsEqualTo(ReactionType.Heart);
    }

    [Test]
    public async Task FromPersistence_WithEyesTypeId_ReconstructsEyesReaction()
    {
        // Arrange
        var entity = new ReactionDatabaseEntity
        {
            PublicId = Guid.NewGuid().ToString(),
            TypeId = (int)ReactionTypeEnum.Eyes,
            CreatedAt = DateTime.UtcNow,
            Post = new PostDatabaseEntity
            {
                PublicId = Guid.NewGuid().ToString(),
                Content = "test",
                CreatedAt = DateTime.UtcNow,
                LastModifiedAt = DateTime.UtcNow
            },
            User = new UserDatabaseEntity
            {
                PublicId = Guid.NewGuid().ToString(),
                DisplayName = "test",
                Email = "test@example.com",
                CreatedAt = DateTime.UtcNow,
                LastModifiedAt = DateTime.UtcNow
            }
        };

        // Act
        var reaction = entity.FromPersistence();

        // Assert
        await Assert.That(reaction.Type).IsEqualTo(ReactionType.Eyes);
    }

    [Test]
    public async Task FromPersistence_ExtractsPostIdFromNavigationProperty()
    {
        // Arrange
        var postPublicId = Guid.NewGuid().ToString();
        var entity = new ReactionDatabaseEntity
        {
            PublicId = Guid.NewGuid().ToString(),
            TypeId = (int)ReactionTypeEnum.ThumbsUp,
            CreatedAt = DateTime.UtcNow,
            Post = new PostDatabaseEntity
            {
                PublicId = postPublicId,
                Content = "test",
                CreatedAt = DateTime.UtcNow,
                LastModifiedAt = DateTime.UtcNow
            },
            User = new UserDatabaseEntity
            {
                PublicId = Guid.NewGuid().ToString(),
                DisplayName = "test",
                Email = "test@example.com",
                CreatedAt = DateTime.UtcNow,
                LastModifiedAt = DateTime.UtcNow
            }
        };

        // Act
        var reaction = entity.FromPersistence();

        // Assert
        await Assert.That(reaction.PostId.Value).IsEqualTo(postPublicId);
    }

    [Test]
    public async Task FromPersistence_ExtractsUserIdFromNavigationProperty()
    {
        // Arrange
        var userPublicId = Guid.NewGuid().ToString();
        var entity = new ReactionDatabaseEntity
        {
            PublicId = Guid.NewGuid().ToString(),
            TypeId = (int)ReactionTypeEnum.Heart,
            CreatedAt = DateTime.UtcNow,
            Post = new PostDatabaseEntity
            {
                PublicId = Guid.NewGuid().ToString(),
                Content = "test",
                CreatedAt = DateTime.UtcNow,
                LastModifiedAt = DateTime.UtcNow
            },
            User = new UserDatabaseEntity
            {
                PublicId = userPublicId,
                DisplayName = "test",
                Email = "test@example.com",
                CreatedAt = DateTime.UtcNow,
                LastModifiedAt = DateTime.UtcNow
            }
        };

        // Act
        var reaction = entity.FromPersistence();

        // Assert
        await Assert.That(reaction.UserId.Value).IsEqualTo(userPublicId);
    }

    #endregion

    #region Round-Trip Tests

    [Test]
    public async Task RoundTrip_WithThumbsUpReaction_PreservesType()
    {
        // Arrange
        var postId = PostId.New();
        var userId = UserId.New();
        var originalReaction = Reaction.Create(postId, userId, ReactionType.ThumbsUp);

        // Act
        var entity = originalReaction.ToPersistence();
        entity.Post = new PostDatabaseEntity
        {
            PublicId = postId.Value,
            Content = "test",
            CreatedAt = DateTime.UtcNow,
            LastModifiedAt = DateTime.UtcNow
        };
        entity.User = new UserDatabaseEntity
        {
            PublicId = userId.Value,
            DisplayName = "test",
            Email = "test@example.com",
            CreatedAt = DateTime.UtcNow,
            LastModifiedAt = DateTime.UtcNow
        };
        var reconstructedReaction = entity.FromPersistence();

        // Assert
        await Assert.That(reconstructedReaction.Type).IsEqualTo(ReactionType.ThumbsUp);
        await Assert.That(reconstructedReaction.PublicId).IsEqualTo(originalReaction.PublicId);
    }

    [Test]
    public async Task RoundTrip_WithHeartReaction_PreservesType()
    {
        // Arrange
        var postId = PostId.New();
        var userId = UserId.New();
        var originalReaction = Reaction.Create(postId, userId, ReactionType.Heart);

        // Act
        var entity = originalReaction.ToPersistence();
        entity.Post = new PostDatabaseEntity
        {
            PublicId = postId.Value,
            Content = "test",
            CreatedAt = DateTime.UtcNow,
            LastModifiedAt = DateTime.UtcNow
        };
        entity.User = new UserDatabaseEntity
        {
            PublicId = userId.Value,
            DisplayName = "test",
            Email = "test@example.com",
            CreatedAt = DateTime.UtcNow,
            LastModifiedAt = DateTime.UtcNow
        };
        var reconstructedReaction = entity.FromPersistence();

        // Assert
        await Assert.That(reconstructedReaction.Type).IsEqualTo(ReactionType.Heart);
    }

    [Test]
    public async Task RoundTrip_WithEyesReaction_PreservesType()
    {
        // Arrange
        var postId = PostId.New();
        var userId = UserId.New();
        var originalReaction = Reaction.Create(postId, userId, ReactionType.Eyes);

        // Act
        var entity = originalReaction.ToPersistence();
        entity.Post = new PostDatabaseEntity
        {
            PublicId = postId.Value,
            Content = "test",
            CreatedAt = DateTime.UtcNow,
            LastModifiedAt = DateTime.UtcNow
        };
        entity.User = new UserDatabaseEntity
        {
            PublicId = userId.Value,
            DisplayName = "test",
            Email = "test@example.com",
            CreatedAt = DateTime.UtcNow,
            LastModifiedAt = DateTime.UtcNow
        };
        var reconstructedReaction = entity.FromPersistence();

        // Assert
        await Assert.That(reconstructedReaction.Type).IsEqualTo(ReactionType.Eyes);
    }

    [Test]
    public async Task RoundTrip_AllReactionTypes_PreserveAllData()
    {
        // Test all reaction types in a single test
        var reactionTypes = new[] { ReactionType.ThumbsUp, ReactionType.Heart, ReactionType.Eyes };

        foreach (var type in reactionTypes)
        {
            // Arrange
            var postId = PostId.New();
            var userId = UserId.New();
            var originalReaction = Reaction.Create(postId, userId, type);

            // Act
            var entity = originalReaction.ToPersistence();
            entity.Post = new PostDatabaseEntity
            {
                PublicId = postId.Value,
                Content = "test",
                CreatedAt = DateTime.UtcNow,
                LastModifiedAt = DateTime.UtcNow
            };
            entity.User = new UserDatabaseEntity
            {
                PublicId = userId.Value,
                DisplayName = "test",
                Email = "test@example.com",
                CreatedAt = DateTime.UtcNow,
                LastModifiedAt = DateTime.UtcNow
            };
            var reconstructedReaction = entity.FromPersistence();

            // Assert
            await Assert.That(reconstructedReaction.Type).IsEqualTo(type);
            await Assert.That(reconstructedReaction.PublicId).IsEqualTo(originalReaction.PublicId);
            await Assert.That(reconstructedReaction.PostId).IsEqualTo(postId);
            await Assert.That(reconstructedReaction.UserId).IsEqualTo(userId);
            await Assert.That(Math.Abs((reconstructedReaction.CreatedAt - originalReaction.CreatedAt).TotalMilliseconds)).IsLessThan(1);
        }
    }

    #endregion

    #region Edge Cases

    [Test]
    public async Task ToPersistence_PreservesCreatedAt()
    {
        // Arrange
        var specificTime = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);
        var reaction = Reaction.Rehydrate(
            ReactionId.New(),
            PostId.New(),
            UserId.New(),
            ReactionType.Heart,
            specificTime);

        // Act
        var entity = reaction.ToPersistence();

        // Assert
        await Assert.That(entity.CreatedAt).IsEqualTo(specificTime);
    }

    [Test]
    public async Task FromPersistence_PreservesCreatedAt()
    {
        // Arrange
        var specificTime = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);
        var entity = new ReactionDatabaseEntity
        {
            PublicId = Guid.NewGuid().ToString(),
            TypeId = (int)ReactionTypeEnum.Eyes,
            CreatedAt = specificTime,
            Post = new PostDatabaseEntity
            {
                PublicId = Guid.NewGuid().ToString(),
                Content = "test",
                CreatedAt = DateTime.UtcNow,
                LastModifiedAt = DateTime.UtcNow
            },
            User = new UserDatabaseEntity
            {
                PublicId = Guid.NewGuid().ToString(),
                DisplayName = "test",
                Email = "test@example.com",
                CreatedAt = DateTime.UtcNow,
                LastModifiedAt = DateTime.UtcNow
            }
        };

        // Act
        var reaction = entity.FromPersistence();

        // Assert
        await Assert.That(reaction.CreatedAt).IsEqualTo(specificTime);
    }

    #endregion
}
