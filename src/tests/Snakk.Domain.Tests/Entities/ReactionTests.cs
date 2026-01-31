using FluentAssertions;
using Snakk.Domain.Entities;
using Snakk.Domain.Events;
using Snakk.Domain.ValueObjects;

namespace Snakk.Domain.Tests.Entities;

public class ReactionTests
{
    [Fact]
    public void Create_WithValidParameters_CreatesReaction()
    {
        // Arrange
        var postId = PostId.New();
        var userId = UserId.New();
        var type = ReactionType.ThumbsUp;

        // Act
        var reaction = Reaction.Create(postId, userId, type);

        // Assert
        reaction.Should().NotBeNull();
        reaction.PublicId.Should().NotBe(Guid.Empty);
        reaction.PostId.Should().Be(postId);
        reaction.UserId.Should().Be(userId);
        reaction.Type.Should().Be(type);
        reaction.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Create_WithThumbsUpType_CreatesThumbsUpReaction()
    {
        // Arrange
        var postId = PostId.New();
        var userId = UserId.New();
        var type = ReactionType.ThumbsUp;

        // Act
        var reaction = Reaction.Create(postId, userId, type);

        // Assert
        reaction.Type.Should().Be(ReactionType.ThumbsUp);
    }

    [Fact]
    public void Create_WithHeartType_CreatesHeartReaction()
    {
        // Arrange
        var postId = PostId.New();
        var userId = UserId.New();
        var type = ReactionType.Heart;

        // Act
        var reaction = Reaction.Create(postId, userId, type);

        // Assert
        reaction.Type.Should().Be(ReactionType.Heart);
    }

    [Fact]
    public void Create_WithEyesType_CreatesEyesReaction()
    {
        // Arrange
        var postId = PostId.New();
        var userId = UserId.New();
        var type = ReactionType.Eyes;

        // Act
        var reaction = Reaction.Create(postId, userId, type);

        // Assert
        reaction.Type.Should().Be(ReactionType.Eyes);
    }

    [Fact]
    public void Create_GeneratesReactionAddedEvent()
    {
        // Arrange
        var postId = PostId.New();
        var userId = UserId.New();
        var type = ReactionType.Heart;

        // Act
        var reaction = Reaction.Create(postId, userId, type);

        // Assert
        reaction.DomainEvents.Should().ContainSingle();
        reaction.DomainEvents.First().Should().BeOfType<ReactionAddedEvent>();

        var addedEvent = reaction.DomainEvents.First() as ReactionAddedEvent;
        addedEvent.Should().NotBeNull();
        addedEvent!.ReactionId.Should().Be(reaction.PublicId);
        addedEvent.PostId.Should().Be(postId);
        addedEvent.UserId.Should().Be(userId);
    }

    [Fact]
    public void Rehydrate_WithValidParameters_RestoresReaction()
    {
        // Arrange
        var reactionId = ReactionId.New();
        var postId = PostId.New();
        var userId = UserId.New();
        var type = ReactionType.Eyes;
        var createdAt = DateTime.UtcNow.AddHours(-1);

        // Act
        var reaction = Reaction.Rehydrate(reactionId, postId, userId, type, createdAt);

        // Assert
        reaction.PublicId.Should().Be(reactionId);
        reaction.PostId.Should().Be(postId);
        reaction.UserId.Should().Be(userId);
        reaction.Type.Should().Be(type);
        reaction.CreatedAt.Should().Be(createdAt);
    }

    [Fact]
    public void Rehydrate_DoesNotGenerateDomainEvents()
    {
        // Arrange
        var reactionId = ReactionId.New();
        var postId = PostId.New();
        var userId = UserId.New();
        var type = ReactionType.ThumbsUp;
        var createdAt = DateTime.UtcNow.AddHours(-1);

        // Act
        var reaction = Reaction.Rehydrate(reactionId, postId, userId, type, createdAt);

        // Assert
        reaction.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void MarkForRemoval_GeneratesReactionRemovedEvent()
    {
        // Arrange
        var postId = PostId.New();
        var userId = UserId.New();
        var reaction = Reaction.Create(postId, userId, ReactionType.Heart);
        reaction.ClearDomainEvents(); // Clear creation event

        // Act
        reaction.MarkForRemoval();

        // Assert
        reaction.DomainEvents.Should().ContainSingle();
        reaction.DomainEvents.First().Should().BeOfType<ReactionRemovedEvent>();

        var removedEvent = reaction.DomainEvents.First() as ReactionRemovedEvent;
        removedEvent.Should().NotBeNull();
        removedEvent!.ReactionId.Should().Be(reaction.PublicId);
        removedEvent.PostId.Should().Be(postId);
        removedEvent.UserId.Should().Be(userId);
    }

    [Fact]
    public void MarkForRemoval_CalledMultipleTimes_GeneratesMultipleEvents()
    {
        // Arrange
        var postId = PostId.New();
        var userId = UserId.New();
        var reaction = Reaction.Create(postId, userId, ReactionType.ThumbsUp);
        reaction.ClearDomainEvents();

        // Act
        reaction.MarkForRemoval();
        reaction.MarkForRemoval();

        // Assert
        reaction.DomainEvents.Should().HaveCount(2);
        reaction.DomainEvents.Should().AllBeOfType<ReactionRemovedEvent>();
    }

    [Fact]
    public void Create_WithDifferentUsers_CreatesDifferentReactions()
    {
        // Arrange
        var postId = PostId.New();
        var userId1 = UserId.New();
        var userId2 = UserId.New();
        var type = ReactionType.Heart;

        // Act
        var reaction1 = Reaction.Create(postId, userId1, type);
        var reaction2 = Reaction.Create(postId, userId2, type);

        // Assert
        reaction1.PublicId.Should().NotBe(reaction2.PublicId);
        reaction1.UserId.Should().Be(userId1);
        reaction2.UserId.Should().Be(userId2);
        reaction1.PostId.Should().Be(postId);
        reaction2.PostId.Should().Be(postId);
    }

    [Fact]
    public void Create_WithDifferentPosts_CreatesDifferentReactions()
    {
        // Arrange
        var postId1 = PostId.New();
        var postId2 = PostId.New();
        var userId = UserId.New();
        var type = ReactionType.Eyes;

        // Act
        var reaction1 = Reaction.Create(postId1, userId, type);
        var reaction2 = Reaction.Create(postId2, userId, type);

        // Assert
        reaction1.PublicId.Should().NotBe(reaction2.PublicId);
        reaction1.PostId.Should().Be(postId1);
        reaction2.PostId.Should().Be(postId2);
        reaction1.UserId.Should().Be(userId);
        reaction2.UserId.Should().Be(userId);
    }

    [Fact]
    public void Create_SameUserSamePostDifferentType_CreatesDifferentReactions()
    {
        // Arrange
        var postId = PostId.New();
        var userId = UserId.New();

        // Act
        var thumbsUp = Reaction.Create(postId, userId, ReactionType.ThumbsUp);
        var heart = Reaction.Create(postId, userId, ReactionType.Heart);
        var eyes = Reaction.Create(postId, userId, ReactionType.Eyes);

        // Assert
        thumbsUp.PublicId.Should().NotBe(heart.PublicId);
        heart.PublicId.Should().NotBe(eyes.PublicId);
        thumbsUp.Type.Should().Be(ReactionType.ThumbsUp);
        heart.Type.Should().Be(ReactionType.Heart);
        eyes.Type.Should().Be(ReactionType.Eyes);
    }

    [Fact]
    public void Rehydrate_PreservesAllProperties()
    {
        // Arrange
        var reactionId = ReactionId.New();
        var postId = PostId.New();
        var userId = UserId.New();
        var type = ReactionType.Heart;
        var createdAt = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);

        // Act
        var reaction = Reaction.Rehydrate(reactionId, postId, userId, type, createdAt);

        // Assert
        reaction.PublicId.Should().Be(reactionId);
        reaction.PostId.Should().Be(postId);
        reaction.UserId.Should().Be(userId);
        reaction.Type.Should().Be(type);
        reaction.CreatedAt.Should().Be(createdAt);
    }

    [Fact]
    public void Create_AssignsUniqueReactionIds()
    {
        // Arrange
        var postId = PostId.New();
        var userId = UserId.New();
        var type = ReactionType.ThumbsUp;

        // Act
        var reaction1 = Reaction.Create(postId, userId, type);
        var reaction2 = Reaction.Create(postId, userId, type);
        var reaction3 = Reaction.Create(postId, userId, type);

        // Assert
        reaction1.PublicId.Should().NotBe(reaction2.PublicId);
        reaction2.PublicId.Should().NotBe(reaction3.PublicId);
        reaction1.PublicId.Should().NotBe(reaction3.PublicId);
    }

    [Fact]
    public void MarkForRemoval_AfterRehydrate_GeneratesEvent()
    {
        // Arrange
        var reactionId = ReactionId.New();
        var postId = PostId.New();
        var userId = UserId.New();
        var reaction = Reaction.Rehydrate(reactionId, postId, userId, ReactionType.Heart, DateTime.UtcNow);

        // Act
        reaction.MarkForRemoval();

        // Assert
        reaction.DomainEvents.Should().ContainSingle();
        reaction.DomainEvents.First().Should().BeOfType<ReactionRemovedEvent>();
    }

    [Fact]
    public void Create_AllReactionTypes_AreSupported()
    {
        // Arrange
        var postId = PostId.New();
        var userId = UserId.New();

        // Act & Assert - All enum values should be creatable
        var thumbsUp = Reaction.Create(postId, userId, ReactionType.ThumbsUp);
        thumbsUp.Type.Should().Be(ReactionType.ThumbsUp);

        var heart = Reaction.Create(postId, userId, ReactionType.Heart);
        heart.Type.Should().Be(ReactionType.Heart);

        var eyes = Reaction.Create(postId, userId, ReactionType.Eyes);
        eyes.Type.Should().Be(ReactionType.Eyes);
    }
}
