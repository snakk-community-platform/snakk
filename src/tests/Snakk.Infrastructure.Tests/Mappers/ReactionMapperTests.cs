using FluentAssertions;
using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Infrastructure.Mappers;
using Snakk.Shared.Enums;

namespace Snakk.Infrastructure.Tests.Mappers;

public class ReactionMapperTests
{
    #region ToPersistence Tests

    [Fact]
    public void ToPersistence_WithThumbsUpReaction_MapsToCorrectTypeId()
    {
        // Arrange
        var reaction = Reaction.Create(PostId.New(), UserId.New(), ReactionType.ThumbsUp);

        // Act
        var entity = reaction.ToPersistence();

        // Assert
        entity.Should().NotBeNull();
        entity.PublicId.Should().Be(reaction.PublicId);
        entity.TypeId.Should().Be((int)ReactionTypeEnum.ThumbsUp);
        entity.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void ToPersistence_WithHeartReaction_MapsToCorrectTypeId()
    {
        // Arrange
        var reaction = Reaction.Create(PostId.New(), UserId.New(), ReactionType.Heart);

        // Act
        var entity = reaction.ToPersistence();

        // Assert
        entity.TypeId.Should().Be((int)ReactionTypeEnum.Heart);
    }

    [Fact]
    public void ToPersistence_WithEyesReaction_MapsToCorrectTypeId()
    {
        // Arrange
        var reaction = Reaction.Create(PostId.New(), UserId.New(), ReactionType.Eyes);

        // Act
        var entity = reaction.ToPersistence();

        // Assert
        entity.TypeId.Should().Be((int)ReactionTypeEnum.Eyes);
    }

    [Fact]
    public void ToPersistence_MapsAllProperties()
    {
        // Arrange
        var postId = PostId.New();
        var userId = UserId.New();
        var reaction = Reaction.Create(postId, userId, ReactionType.Heart);

        // Act
        var entity = reaction.ToPersistence();

        // Assert
        entity.PublicId.Should().Be(reaction.PublicId);
        entity.TypeId.Should().Be((int)ReactionTypeEnum.Heart);
        entity.CreatedAt.Should().Be(reaction.CreatedAt);
    }

    #endregion

    #region FromPersistence Tests

    [Fact]
    public void FromPersistence_WithThumbsUpTypeId_ReconstructsThumbsUpReaction()
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
        reaction.Should().NotBeNull();
        reaction.PublicId.Value.Should().Be(entity.PublicId);
        reaction.Type.Should().Be(ReactionType.ThumbsUp);
        reaction.PostId.Value.Should().Be(postPublicId);
        reaction.UserId.Value.Should().Be(userPublicId);
        reaction.CreatedAt.Should().Be(entity.CreatedAt);
    }

    [Fact]
    public void FromPersistence_WithHeartTypeId_ReconstructsHeartReaction()
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
        reaction.Type.Should().Be(ReactionType.Heart);
    }

    [Fact]
    public void FromPersistence_WithEyesTypeId_ReconstructsEyesReaction()
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
        reaction.Type.Should().Be(ReactionType.Eyes);
    }

    [Fact]
    public void FromPersistence_ExtractsPostIdFromNavigationProperty()
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
        reaction.PostId.Value.Should().Be(postPublicId);
    }

    [Fact]
    public void FromPersistence_ExtractsUserIdFromNavigationProperty()
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
        reaction.UserId.Value.Should().Be(userPublicId);
    }

    #endregion

    #region Round-Trip Tests

    [Fact]
    public void RoundTrip_WithThumbsUpReaction_PreservesType()
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
        reconstructedReaction.Type.Should().Be(ReactionType.ThumbsUp);
        reconstructedReaction.PublicId.Should().Be(originalReaction.PublicId);
    }

    [Fact]
    public void RoundTrip_WithHeartReaction_PreservesType()
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
        reconstructedReaction.Type.Should().Be(ReactionType.Heart);
    }

    [Fact]
    public void RoundTrip_WithEyesReaction_PreservesType()
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
        reconstructedReaction.Type.Should().Be(ReactionType.Eyes);
    }

    [Fact]
    public void RoundTrip_AllReactionTypes_PreserveAllData()
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
            reconstructedReaction.Type.Should().Be(type, $"reaction type {type} should be preserved");
            reconstructedReaction.PublicId.Should().Be(originalReaction.PublicId);
            reconstructedReaction.PostId.Should().Be(postId);
            reconstructedReaction.UserId.Should().Be(userId);
            reconstructedReaction.CreatedAt.Should().BeCloseTo(originalReaction.CreatedAt, TimeSpan.FromMilliseconds(1));
        }
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void ToPersistence_PreservesCreatedAt()
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
        entity.CreatedAt.Should().Be(specificTime);
    }

    [Fact]
    public void FromPersistence_PreservesCreatedAt()
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
        reaction.CreatedAt.Should().Be(specificTime);
    }

    #endregion
}
