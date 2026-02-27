using Snakk.Domain.Entities;
using Snakk.Domain.Events;
using Snakk.Domain.ValueObjects;

namespace Snakk.Domain.Tests.Entities;

public class FollowEdgeCasesTests
{
    #region CreateForDiscussion Always Sets Level to DiscussionsAndPosts

    [Test]
    public async Task CreateForDiscussion_AlwaysSetsLevelToDiscussionsAndPosts()
    {
        // Arrange
        var userId = UserId.New();
        var discussionId = DiscussionId.New();

        // Act
        var follow = Follow.CreateForDiscussion(userId, discussionId);

        // Assert
        await Assert.That(follow.Level).IsEqualTo(FollowLevel.DiscussionsAndPosts);
    }

    [Test]
    public async Task CreateForDiscussion_GeneratesFollowCreatedEvent()
    {
        // Arrange
        var userId = UserId.New();
        var discussionId = DiscussionId.New();

        // Act
        var follow = Follow.CreateForDiscussion(userId, discussionId);

        // Assert
        await Assert.That(follow.DomainEvents).Count().IsEqualTo(1);
        var createdEvent = follow.DomainEvents.First() as FollowCreatedEvent;
        await Assert.That(createdEvent).IsNotNull();
        await Assert.That(createdEvent!.UserId).IsEqualTo(userId);
        await Assert.That(createdEvent.TargetType).IsEqualTo(FollowTargetType.Discussion);
        await Assert.That(createdEvent.TargetId).IsEqualTo(discussionId.Value);
    }

    #endregion

    #region CreateForSpace With Custom Level

    [Test]
    public async Task CreateForSpace_WithDiscussionsAndPosts_SetsLevel()
    {
        // Arrange
        var userId = UserId.New();
        var spaceId = SpaceId.New();

        // Act
        var follow = Follow.CreateForSpace(userId, spaceId, FollowLevel.DiscussionsAndPosts);

        // Assert
        await Assert.That(follow.Level).IsEqualTo(FollowLevel.DiscussionsAndPosts);
    }

    [Test]
    public async Task CreateForSpace_DefaultsToDiscussionsOnly()
    {
        // Arrange
        var userId = UserId.New();
        var spaceId = SpaceId.New();

        // Act
        var follow = Follow.CreateForSpace(userId, spaceId);

        // Assert
        await Assert.That(follow.Level).IsEqualTo(FollowLevel.DiscussionsOnly);
    }

    [Test]
    public async Task CreateForSpace_GeneratesFollowCreatedEvent()
    {
        // Arrange
        var userId = UserId.New();
        var spaceId = SpaceId.New();

        // Act
        var follow = Follow.CreateForSpace(userId, spaceId);

        // Assert
        await Assert.That(follow.DomainEvents).Count().IsEqualTo(1);
        var createdEvent = follow.DomainEvents.First() as FollowCreatedEvent;
        await Assert.That(createdEvent).IsNotNull();
        await Assert.That(createdEvent!.TargetType).IsEqualTo(FollowTargetType.Space);
        await Assert.That(createdEvent.TargetId).IsEqualTo(spaceId.Value);
    }

    #endregion

    #region CreateForUser Always Sets Level to DiscussionsAndPosts

    [Test]
    public async Task CreateForUser_AlwaysSetsLevelToDiscussionsAndPosts()
    {
        // Arrange
        var userId = UserId.New();
        var followedUserId = UserId.New();

        // Act
        var follow = Follow.CreateForUser(userId, followedUserId);

        // Assert
        await Assert.That(follow.Level).IsEqualTo(FollowLevel.DiscussionsAndPosts);
    }

    [Test]
    public async Task CreateForUser_GeneratesFollowCreatedEvent()
    {
        // Arrange
        var userId = UserId.New();
        var followedUserId = UserId.New();

        // Act
        var follow = Follow.CreateForUser(userId, followedUserId);

        // Assert
        await Assert.That(follow.DomainEvents).Count().IsEqualTo(1);
        var createdEvent = follow.DomainEvents.First() as FollowCreatedEvent;
        await Assert.That(createdEvent).IsNotNull();
        await Assert.That(createdEvent!.TargetType).IsEqualTo(FollowTargetType.User);
        await Assert.That(createdEvent.TargetId).IsEqualTo(followedUserId.Value);
    }

    #endregion

    #region UpdateLevel Changes Level

    [Test]
    public async Task UpdateLevel_FromDiscussionsOnlyToDiscussionsAndPosts_ChangesLevel()
    {
        // Arrange
        var follow = Follow.CreateForSpace(UserId.New(), SpaceId.New(), FollowLevel.DiscussionsOnly);

        // Act
        follow.UpdateLevel(FollowLevel.DiscussionsAndPosts);

        // Assert
        await Assert.That(follow.Level).IsEqualTo(FollowLevel.DiscussionsAndPosts);
    }

    [Test]
    public async Task UpdateLevel_FromDiscussionsAndPostsToDiscussionsOnly_ChangesLevel()
    {
        // Arrange
        var follow = Follow.CreateForSpace(UserId.New(), SpaceId.New(), FollowLevel.DiscussionsAndPosts);

        // Act
        follow.UpdateLevel(FollowLevel.DiscussionsOnly);

        // Assert
        await Assert.That(follow.Level).IsEqualTo(FollowLevel.DiscussionsOnly);
    }

    [Test]
    public async Task UpdateLevel_WithSameLevel_IsIdempotent()
    {
        // Arrange
        var follow = Follow.CreateForSpace(UserId.New(), SpaceId.New(), FollowLevel.DiscussionsOnly);

        // Act
        follow.UpdateLevel(FollowLevel.DiscussionsOnly);

        // Assert
        await Assert.That(follow.Level).IsEqualTo(FollowLevel.DiscussionsOnly);
    }

    #endregion

    #region MarkForRemoval Resolves Correct TargetId

    [Test]
    public async Task MarkForRemoval_ForDiscussionFollow_UsesDiscussionId()
    {
        // Arrange
        var userId = UserId.New();
        var discussionId = DiscussionId.New();
        var follow = Follow.CreateForDiscussion(userId, discussionId);
        follow.ClearDomainEvents();

        // Act
        follow.MarkForRemoval();

        // Assert
        await Assert.That(follow.DomainEvents).Count().IsEqualTo(1);
        var removedEvent = follow.DomainEvents.First() as FollowRemovedEvent;
        await Assert.That(removedEvent).IsNotNull();
        await Assert.That(removedEvent!.TargetId).IsEqualTo(discussionId.Value);
        await Assert.That(removedEvent.TargetType).IsEqualTo(FollowTargetType.Discussion);
        await Assert.That(removedEvent.UserId).IsEqualTo(userId);
    }

    [Test]
    public async Task MarkForRemoval_ForSpaceFollow_UsesSpaceId()
    {
        // Arrange
        var userId = UserId.New();
        var spaceId = SpaceId.New();
        var follow = Follow.CreateForSpace(userId, spaceId);
        follow.ClearDomainEvents();

        // Act
        follow.MarkForRemoval();

        // Assert
        var removedEvent = follow.DomainEvents.First() as FollowRemovedEvent;
        await Assert.That(removedEvent).IsNotNull();
        await Assert.That(removedEvent!.TargetId).IsEqualTo(spaceId.Value);
        await Assert.That(removedEvent.TargetType).IsEqualTo(FollowTargetType.Space);
    }

    [Test]
    public async Task MarkForRemoval_ForUserFollow_UsesFollowedUserId()
    {
        // Arrange
        var userId = UserId.New();
        var followedUserId = UserId.New();
        var follow = Follow.CreateForUser(userId, followedUserId);
        follow.ClearDomainEvents();

        // Act
        follow.MarkForRemoval();

        // Assert
        var removedEvent = follow.DomainEvents.First() as FollowRemovedEvent;
        await Assert.That(removedEvent).IsNotNull();
        await Assert.That(removedEvent!.TargetId).IsEqualTo(followedUserId.Value);
        await Assert.That(removedEvent.TargetType).IsEqualTo(FollowTargetType.User);
    }

    [Test]
    public async Task MarkForRemoval_IncludesFollowIdInEvent()
    {
        // Arrange
        var userId = UserId.New();
        var discussionId = DiscussionId.New();
        var follow = Follow.CreateForDiscussion(userId, discussionId);
        follow.ClearDomainEvents();

        // Act
        follow.MarkForRemoval();

        // Assert
        var removedEvent = follow.DomainEvents.First() as FollowRemovedEvent;
        await Assert.That(removedEvent!.FollowId).IsEqualTo(follow.PublicId);
    }

    #endregion

    #region MarkForRemoval With Rehydrated Follows

    [Test]
    public async Task MarkForRemoval_RehydratedDiscussionFollow_ResolvesCorrectId()
    {
        // Arrange
        var followId = FollowId.New();
        var userId = UserId.New();
        var discussionId = DiscussionId.New();
        var follow = Follow.Rehydrate(
            followId, userId, FollowTargetType.Discussion,
            discussionId, null, null,
            FollowLevel.DiscussionsAndPosts, DateTime.UtcNow);

        // Act
        follow.MarkForRemoval();

        // Assert
        var removedEvent = follow.DomainEvents.First() as FollowRemovedEvent;
        await Assert.That(removedEvent!.TargetId).IsEqualTo(discussionId.Value);
    }

    [Test]
    public async Task MarkForRemoval_RehydratedSpaceFollow_ResolvesCorrectId()
    {
        // Arrange
        var followId = FollowId.New();
        var userId = UserId.New();
        var spaceId = SpaceId.New();
        var follow = Follow.Rehydrate(
            followId, userId, FollowTargetType.Space,
            null, spaceId, null,
            FollowLevel.DiscussionsOnly, DateTime.UtcNow);

        // Act
        follow.MarkForRemoval();

        // Assert
        var removedEvent = follow.DomainEvents.First() as FollowRemovedEvent;
        await Assert.That(removedEvent!.TargetId).IsEqualTo(spaceId.Value);
    }

    [Test]
    public async Task MarkForRemoval_RehydratedUserFollow_ResolvesCorrectId()
    {
        // Arrange
        var followId = FollowId.New();
        var userId = UserId.New();
        var followedUserId = UserId.New();
        var follow = Follow.Rehydrate(
            followId, userId, FollowTargetType.User,
            null, null, followedUserId,
            FollowLevel.DiscussionsAndPosts, DateTime.UtcNow);

        // Act
        follow.MarkForRemoval();

        // Assert
        var removedEvent = follow.DomainEvents.First() as FollowRemovedEvent;
        await Assert.That(removedEvent!.TargetId).IsEqualTo(followedUserId.Value);
    }

    #endregion

    #region ClearDomainEvents

    [Test]
    public async Task ClearDomainEvents_ClearsAllEvents()
    {
        // Arrange
        var follow = Follow.CreateForDiscussion(UserId.New(), DiscussionId.New());
        await Assert.That(follow.DomainEvents).Count().IsEqualTo(1);

        // Act
        follow.ClearDomainEvents();

        // Assert
        await Assert.That(follow.DomainEvents).IsEmpty();
    }

    [Test]
    public async Task ClearDomainEvents_ThenMarkForRemoval_HasOnlyRemovalEvent()
    {
        // Arrange
        var follow = Follow.CreateForDiscussion(UserId.New(), DiscussionId.New());
        follow.ClearDomainEvents();

        // Act
        follow.MarkForRemoval();

        // Assert
        await Assert.That(follow.DomainEvents).Count().IsEqualTo(1);
        await Assert.That(follow.DomainEvents.First()).IsTypeOf<FollowRemovedEvent>();
    }

    #endregion
}
