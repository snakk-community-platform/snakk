using Snakk.Domain.Entities;
using Snakk.Domain.Events;
using Snakk.Domain.ValueObjects;

namespace Snakk.Domain.Tests.Entities;

public class ReactionTests
{
    [Test]
    public async Task Create_WithValidParameters_CreatesReaction()
    {
        // Arrange
        var postId = PostId.New();
        var userId = UserId.New();
        var type = ReactionType.Agree;

        // Act
        var reaction = Reaction.Create(postId, userId, type);

        // Assert
        await Assert.That(reaction).IsNotNull();
        await Assert.That(reaction.PublicId).IsNotNull();
        await Assert.That(reaction.PostId).IsEqualTo(postId);
        await Assert.That(reaction.UserId).IsEqualTo(userId);
        await Assert.That(reaction.Type).IsEqualTo(type);
        await Assert.That(reaction.CreatedAt).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task Create_WithAgreeType_CreatesAgreeReaction()
    {
        // Arrange
        var postId = PostId.New();
        var userId = UserId.New();
        var type = ReactionType.Agree;

        // Act
        var reaction = Reaction.Create(postId, userId, type);

        // Assert
        await Assert.That(reaction.Type).IsEqualTo(ReactionType.Agree);
    }

    [Test]
    public async Task Create_WithLoveType_CreatesLoveReaction()
    {
        // Arrange
        var postId = PostId.New();
        var userId = UserId.New();
        var type = ReactionType.Love;

        // Act
        var reaction = Reaction.Create(postId, userId, type);

        // Assert
        await Assert.That(reaction.Type).IsEqualTo(ReactionType.Love);
    }

    [Test]
    public async Task Create_WithWatchingType_CreatesWatchingReaction()
    {
        // Arrange
        var postId = PostId.New();
        var userId = UserId.New();
        var type = ReactionType.Watching;

        // Act
        var reaction = Reaction.Create(postId, userId, type);

        // Assert
        await Assert.That(reaction.Type).IsEqualTo(ReactionType.Watching);
    }

    [Test]
    public async Task Create_GeneratesReactionAddedEvent()
    {
        // Arrange
        var postId = PostId.New();
        var userId = UserId.New();
        var type = ReactionType.Love;

        // Act
        var reaction = Reaction.Create(postId, userId, type);

        // Assert
        await Assert.That(reaction.DomainEvents).Count().IsEqualTo(1);
        await Assert.That(reaction.DomainEvents.First()).IsTypeOf<ReactionAddedEvent>();

        var addedEvent = reaction.DomainEvents.First() as ReactionAddedEvent;
        await Assert.That(addedEvent).IsNotNull();
        await Assert.That(addedEvent!.ReactionId).IsEqualTo(reaction.PublicId);
        await Assert.That(addedEvent.PostId).IsEqualTo(postId);
        await Assert.That(addedEvent.UserId).IsEqualTo(userId);
    }

    [Test]
    public async Task Rehydrate_WithValidParameters_RestoresReaction()
    {
        // Arrange
        var reactionId = ReactionId.New();
        var postId = PostId.New();
        var userId = UserId.New();
        var type = ReactionType.Watching;
        var createdAt = DateTime.UtcNow.AddHours(-1);

        // Act
        var reaction = Reaction.Rehydrate(reactionId, postId, userId, type, createdAt);

        // Assert
        await Assert.That(reaction.PublicId).IsEqualTo(reactionId);
        await Assert.That(reaction.PostId).IsEqualTo(postId);
        await Assert.That(reaction.UserId).IsEqualTo(userId);
        await Assert.That(reaction.Type).IsEqualTo(type);
        await Assert.That(reaction.CreatedAt).IsEqualTo(createdAt);
    }

    [Test]
    public async Task Rehydrate_DoesNotGenerateDomainEvents()
    {
        // Arrange
        var reactionId = ReactionId.New();
        var postId = PostId.New();
        var userId = UserId.New();
        var type = ReactionType.Agree;
        var createdAt = DateTime.UtcNow.AddHours(-1);

        // Act
        var reaction = Reaction.Rehydrate(reactionId, postId, userId, type, createdAt);

        // Assert
        await Assert.That(reaction.DomainEvents).IsEmpty();
    }

    [Test]
    public async Task MarkForRemoval_GeneratesReactionRemovedEvent()
    {
        // Arrange
        var postId = PostId.New();
        var userId = UserId.New();
        var reaction = Reaction.Create(postId, userId, ReactionType.Love);
        reaction.ClearDomainEvents(); // Clear creation event

        // Act
        reaction.MarkForRemoval();

        // Assert
        await Assert.That(reaction.DomainEvents).Count().IsEqualTo(1);
        await Assert.That(reaction.DomainEvents.First()).IsTypeOf<ReactionRemovedEvent>();

        var removedEvent = reaction.DomainEvents.First() as ReactionRemovedEvent;
        await Assert.That(removedEvent).IsNotNull();
        await Assert.That(removedEvent!.ReactionId).IsEqualTo(reaction.PublicId);
        await Assert.That(removedEvent.PostId).IsEqualTo(postId);
        await Assert.That(removedEvent.UserId).IsEqualTo(userId);
    }

    [Test]
    public async Task MarkForRemoval_CalledMultipleTimes_GeneratesMultipleEvents()
    {
        // Arrange
        var postId = PostId.New();
        var userId = UserId.New();
        var reaction = Reaction.Create(postId, userId, ReactionType.Agree);
        reaction.ClearDomainEvents();

        // Act
        reaction.MarkForRemoval();
        reaction.MarkForRemoval();

        // Assert
        await Assert.That(reaction.DomainEvents).Count().IsEqualTo(2);
    }

    [Test]
    public async Task Create_WithDifferentUsers_CreatesDifferentReactions()
    {
        // Arrange
        var postId = PostId.New();
        var userId1 = UserId.New();
        var userId2 = UserId.New();
        var type = ReactionType.Love;

        // Act
        var reaction1 = Reaction.Create(postId, userId1, type);
        var reaction2 = Reaction.Create(postId, userId2, type);

        // Assert
        await Assert.That(reaction1.PublicId).IsNotEqualTo(reaction2.PublicId);
        await Assert.That(reaction1.UserId).IsEqualTo(userId1);
        await Assert.That(reaction2.UserId).IsEqualTo(userId2);
        await Assert.That(reaction1.PostId).IsEqualTo(postId);
        await Assert.That(reaction2.PostId).IsEqualTo(postId);
    }

    [Test]
    public async Task Create_WithDifferentPosts_CreatesDifferentReactions()
    {
        // Arrange
        var postId1 = PostId.New();
        var postId2 = PostId.New();
        var userId = UserId.New();
        var type = ReactionType.Watching;

        // Act
        var reaction1 = Reaction.Create(postId1, userId, type);
        var reaction2 = Reaction.Create(postId2, userId, type);

        // Assert
        await Assert.That(reaction1.PublicId).IsNotEqualTo(reaction2.PublicId);
        await Assert.That(reaction1.PostId).IsEqualTo(postId1);
        await Assert.That(reaction2.PostId).IsEqualTo(postId2);
        await Assert.That(reaction1.UserId).IsEqualTo(userId);
        await Assert.That(reaction2.UserId).IsEqualTo(userId);
    }

    [Test]
    public async Task Create_SameUserSamePostDifferentType_CreatesDifferentReactions()
    {
        // Arrange
        var postId = PostId.New();
        var userId = UserId.New();

        // Act
        var agree = Reaction.Create(postId, userId, ReactionType.Agree);
        var love = Reaction.Create(postId, userId, ReactionType.Love);
        var watching = Reaction.Create(postId, userId, ReactionType.Watching);

        // Assert
        await Assert.That(agree.PublicId).IsNotEqualTo(love.PublicId);
        await Assert.That(love.PublicId).IsNotEqualTo(watching.PublicId);
        await Assert.That(agree.Type).IsEqualTo(ReactionType.Agree);
        await Assert.That(love.Type).IsEqualTo(ReactionType.Love);
        await Assert.That(watching.Type).IsEqualTo(ReactionType.Watching);
    }

    [Test]
    public async Task Rehydrate_PreservesAllProperties()
    {
        // Arrange
        var reactionId = ReactionId.New();
        var postId = PostId.New();
        var userId = UserId.New();
        var type = ReactionType.Love;
        var createdAt = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);

        // Act
        var reaction = Reaction.Rehydrate(reactionId, postId, userId, type, createdAt);

        // Assert
        await Assert.That(reaction.PublicId).IsEqualTo(reactionId);
        await Assert.That(reaction.PostId).IsEqualTo(postId);
        await Assert.That(reaction.UserId).IsEqualTo(userId);
        await Assert.That(reaction.Type).IsEqualTo(type);
        await Assert.That(reaction.CreatedAt).IsEqualTo(createdAt);
    }

    [Test]
    public async Task Create_AssignsUniqueReactionIds()
    {
        // Arrange
        var postId = PostId.New();
        var userId = UserId.New();
        var type = ReactionType.Agree;

        // Act
        var reaction1 = Reaction.Create(postId, userId, type);
        var reaction2 = Reaction.Create(postId, userId, type);
        var reaction3 = Reaction.Create(postId, userId, type);

        // Assert
        await Assert.That(reaction1.PublicId).IsNotEqualTo(reaction2.PublicId);
        await Assert.That(reaction2.PublicId).IsNotEqualTo(reaction3.PublicId);
        await Assert.That(reaction1.PublicId).IsNotEqualTo(reaction3.PublicId);
    }

    [Test]
    public async Task MarkForRemoval_AfterRehydrate_GeneratesEvent()
    {
        // Arrange
        var reactionId = ReactionId.New();
        var postId = PostId.New();
        var userId = UserId.New();
        var reaction = Reaction.Rehydrate(reactionId, postId, userId, ReactionType.Love, DateTime.UtcNow);

        // Act
        reaction.MarkForRemoval();

        // Assert
        await Assert.That(reaction.DomainEvents).Count().IsEqualTo(1);
        await Assert.That(reaction.DomainEvents.First()).IsTypeOf<ReactionRemovedEvent>();
    }

    [Test]
    public async Task Create_AllReactionTypes_AreSupported()
    {
        // Arrange
        var postId = PostId.New();
        var userId = UserId.New();

        // Act & Assert - All enum values should be creatable
        var agree = Reaction.Create(postId, userId, ReactionType.Agree);
        await Assert.That(agree.Type).IsEqualTo(ReactionType.Agree);

        var love = Reaction.Create(postId, userId, ReactionType.Love);
        await Assert.That(love.Type).IsEqualTo(ReactionType.Love);

        var watching = Reaction.Create(postId, userId, ReactionType.Watching);
        await Assert.That(watching.Type).IsEqualTo(ReactionType.Watching);
    }
}
