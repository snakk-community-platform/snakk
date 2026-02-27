using Snakk.Domain.Entities;
using Snakk.Domain.Events;
using Snakk.Domain.ValueObjects;

namespace Snakk.Domain.Tests.Entities;

public class FollowTests
{
    #region CreateForDiscussion Tests

    [Test]
    public async Task CreateForDiscussion_WithValidParameters_CreatesFollowWithDiscussionsAndPostsLevel()
    {
        // Arrange
        var userId = UserId.New();
        var discussionId = DiscussionId.New();

        // Act
        var follow = Follow.CreateForDiscussion(userId, discussionId);

        // Assert
        await Assert.That(follow).IsNotNull();
        await Assert.That(follow.PublicId).IsNotNull();
        await Assert.That(follow.UserId).IsEqualTo(userId);
        await Assert.That(follow.TargetType).IsEqualTo(FollowTargetType.Discussion);
        await Assert.That(follow.DiscussionId).IsEqualTo(discussionId);
        await Assert.That((object?)follow.SpaceId).IsNull();
        await Assert.That((object?)follow.FollowedUserId).IsNull();
        await Assert.That(follow.Level).IsEqualTo(FollowLevel.DiscussionsAndPosts);
        await Assert.That(follow.CreatedAt).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task CreateForDiscussion_AlwaysUsesDiscussionsAndPostsLevel()
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
    public async Task CreateForDiscussion_SetsCorrectTargetType()
    {
        // Arrange
        var userId = UserId.New();
        var discussionId = DiscussionId.New();

        // Act
        var follow = Follow.CreateForDiscussion(userId, discussionId);

        // Assert
        await Assert.That(follow.TargetType).IsEqualTo(FollowTargetType.Discussion);
    }

    #endregion

    #region CreateForSpace Tests

    [Test]
    public async Task CreateForSpace_WithDefaultLevel_CreatesFollowWithDiscussionsOnlyLevel()
    {
        // Arrange
        var userId = UserId.New();
        var spaceId = SpaceId.New();

        // Act
        var follow = Follow.CreateForSpace(userId, spaceId);

        // Assert
        await Assert.That(follow).IsNotNull();
        await Assert.That(follow.PublicId).IsNotNull();
        await Assert.That(follow.UserId).IsEqualTo(userId);
        await Assert.That(follow.TargetType).IsEqualTo(FollowTargetType.Space);
        await Assert.That(follow.SpaceId).IsEqualTo(spaceId);
        await Assert.That((object?)follow.DiscussionId).IsNull();
        await Assert.That((object?)follow.FollowedUserId).IsNull();
        await Assert.That(follow.Level).IsEqualTo(FollowLevel.DiscussionsOnly);
        await Assert.That(follow.CreatedAt).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task CreateForSpace_WithDiscussionsOnlyLevel_CreatesFollowWithDiscussionsOnlyLevel()
    {
        // Arrange
        var userId = UserId.New();
        var spaceId = SpaceId.New();

        // Act
        var follow = Follow.CreateForSpace(userId, spaceId, FollowLevel.DiscussionsOnly);

        // Assert
        await Assert.That(follow.Level).IsEqualTo(FollowLevel.DiscussionsOnly);
    }

    [Test]
    public async Task CreateForSpace_WithDiscussionsAndPostsLevel_CreatesFollowWithDiscussionsAndPostsLevel()
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
    public async Task CreateForSpace_SetsCorrectTargetType()
    {
        // Arrange
        var userId = UserId.New();
        var spaceId = SpaceId.New();

        // Act
        var follow = Follow.CreateForSpace(userId, spaceId);

        // Assert
        await Assert.That(follow.TargetType).IsEqualTo(FollowTargetType.Space);
    }

    #endregion

    #region CreateForUser Tests

    [Test]
    public async Task CreateForUser_WithValidParameters_CreatesFollowWithDiscussionsAndPostsLevel()
    {
        // Arrange
        var userId = UserId.New();
        var followedUserId = UserId.New();

        // Act
        var follow = Follow.CreateForUser(userId, followedUserId);

        // Assert
        await Assert.That(follow).IsNotNull();
        await Assert.That(follow.PublicId).IsNotNull();
        await Assert.That(follow.UserId).IsEqualTo(userId);
        await Assert.That(follow.TargetType).IsEqualTo(FollowTargetType.User);
        await Assert.That(follow.FollowedUserId).IsEqualTo(followedUserId);
        await Assert.That((object?)follow.DiscussionId).IsNull();
        await Assert.That((object?)follow.SpaceId).IsNull();
        await Assert.That(follow.Level).IsEqualTo(FollowLevel.DiscussionsAndPosts);
        await Assert.That(follow.CreatedAt).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task CreateForUser_AlwaysUsesDiscussionsAndPostsLevel()
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
    public async Task CreateForUser_SetsCorrectTargetType()
    {
        // Arrange
        var userId = UserId.New();
        var followedUserId = UserId.New();

        // Act
        var follow = Follow.CreateForUser(userId, followedUserId);

        // Assert
        await Assert.That(follow.TargetType).IsEqualTo(FollowTargetType.User);
    }

    #endregion

    #region UpdateLevel Tests

    [Test]
    public async Task UpdateLevel_OnSpaceFollow_UpdatesLevel()
    {
        // Arrange
        var follow = Follow.CreateForSpace(UserId.New(), SpaceId.New(), FollowLevel.DiscussionsOnly);

        // Act
        follow.UpdateLevel(FollowLevel.DiscussionsAndPosts);

        // Assert
        await Assert.That(follow.Level).IsEqualTo(FollowLevel.DiscussionsAndPosts);
    }

    [Test]
    public async Task UpdateLevel_CanToggleBetweenLevels()
    {
        // Arrange
        var follow = Follow.CreateForSpace(UserId.New(), SpaceId.New(), FollowLevel.DiscussionsOnly);

        // Act & Assert
        await Assert.That(follow.Level).IsEqualTo(FollowLevel.DiscussionsOnly);

        follow.UpdateLevel(FollowLevel.DiscussionsAndPosts);
        await Assert.That(follow.Level).IsEqualTo(FollowLevel.DiscussionsAndPosts);

        follow.UpdateLevel(FollowLevel.DiscussionsOnly);
        await Assert.That(follow.Level).IsEqualTo(FollowLevel.DiscussionsOnly);
    }

    [Test]
    public async Task UpdateLevel_WithSameLevel_DoesNotThrow()
    {
        // Arrange
        var follow = Follow.CreateForSpace(UserId.New(), SpaceId.New(), FollowLevel.DiscussionsOnly);

        // Act & Assert
        await Assert.That(() => follow.UpdateLevel(FollowLevel.DiscussionsOnly)).ThrowsNothing();
        await Assert.That(follow.Level).IsEqualTo(FollowLevel.DiscussionsOnly);
    }

    #endregion

    #region MarkForRemoval Tests

    [Test]
    public async Task MarkForRemoval_OnDiscussionFollow_GeneratesFollowRemovedEventWithDiscussionId()
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
        await Assert.That(removedEvent!.UserId).IsEqualTo(userId);
        await Assert.That(removedEvent.TargetId).IsEqualTo(discussionId.Value);
        await Assert.That(removedEvent.TargetType).IsEqualTo(FollowTargetType.Discussion);
    }

    [Test]
    public async Task MarkForRemoval_OnSpaceFollow_GeneratesFollowRemovedEventWithSpaceId()
    {
        // Arrange
        var userId = UserId.New();
        var spaceId = SpaceId.New();
        var follow = Follow.CreateForSpace(userId, spaceId);
        follow.ClearDomainEvents();

        // Act
        follow.MarkForRemoval();

        // Assert
        await Assert.That(follow.DomainEvents).Count().IsEqualTo(1);
        var removedEvent = follow.DomainEvents.First() as FollowRemovedEvent;
        await Assert.That(removedEvent).IsNotNull();
        await Assert.That(removedEvent!.UserId).IsEqualTo(userId);
        await Assert.That(removedEvent.TargetId).IsEqualTo(spaceId.Value);
        await Assert.That(removedEvent.TargetType).IsEqualTo(FollowTargetType.Space);
    }

    [Test]
    public async Task MarkForRemoval_OnUserFollow_GeneratesFollowRemovedEventWithFollowedUserId()
    {
        // Arrange
        var userId = UserId.New();
        var followedUserId = UserId.New();
        var follow = Follow.CreateForUser(userId, followedUserId);
        follow.ClearDomainEvents();

        // Act
        follow.MarkForRemoval();

        // Assert
        await Assert.That(follow.DomainEvents).Count().IsEqualTo(1);
        var removedEvent = follow.DomainEvents.First() as FollowRemovedEvent;
        await Assert.That(removedEvent).IsNotNull();
        await Assert.That(removedEvent!.UserId).IsEqualTo(userId);
        await Assert.That(removedEvent.TargetId).IsEqualTo(followedUserId.Value);
        await Assert.That(removedEvent.TargetType).IsEqualTo(FollowTargetType.User);
    }

    #endregion

    #region Rehydrate Tests

    [Test]
    public async Task Rehydrate_ForDiscussion_RestoresFollow()
    {
        // Arrange
        var followId = FollowId.New();
        var userId = UserId.New();
        var discussionId = DiscussionId.New();
        var createdAt = DateTime.UtcNow.AddDays(-1);

        // Act
        var follow = Follow.Rehydrate(
            followId,
            userId,
            FollowTargetType.Discussion,
            discussionId,
            null,
            null,
            FollowLevel.DiscussionsAndPosts,
            createdAt);

        // Assert
        await Assert.That(follow.PublicId).IsEqualTo(followId);
        await Assert.That(follow.UserId).IsEqualTo(userId);
        await Assert.That(follow.TargetType).IsEqualTo(FollowTargetType.Discussion);
        await Assert.That(follow.Level).IsEqualTo(FollowLevel.DiscussionsAndPosts);
        await Assert.That(follow.DiscussionId).IsEqualTo(discussionId);
        await Assert.That((object?)follow.SpaceId).IsNull();
        await Assert.That((object?)follow.FollowedUserId).IsNull();
        await Assert.That(follow.CreatedAt).IsEqualTo(createdAt);
    }

    [Test]
    public async Task Rehydrate_ForSpace_RestoresFollow()
    {
        // Arrange
        var followId = FollowId.New();
        var userId = UserId.New();
        var spaceId = SpaceId.New();
        var createdAt = DateTime.UtcNow.AddDays(-1);

        // Act
        var follow = Follow.Rehydrate(
            followId,
            userId,
            FollowTargetType.Space,
            null,
            spaceId,
            null,
            FollowLevel.DiscussionsOnly,
            createdAt);

        // Assert
        await Assert.That(follow.PublicId).IsEqualTo(followId);
        await Assert.That(follow.UserId).IsEqualTo(userId);
        await Assert.That(follow.TargetType).IsEqualTo(FollowTargetType.Space);
        await Assert.That(follow.Level).IsEqualTo(FollowLevel.DiscussionsOnly);
        await Assert.That((object?)follow.DiscussionId).IsNull();
        await Assert.That(follow.SpaceId).IsEqualTo(spaceId);
        await Assert.That((object?)follow.FollowedUserId).IsNull();
        await Assert.That(follow.CreatedAt).IsEqualTo(createdAt);
    }

    [Test]
    public async Task Rehydrate_ForUser_RestoresFollow()
    {
        // Arrange
        var followId = FollowId.New();
        var userId = UserId.New();
        var followedUserId = UserId.New();
        var createdAt = DateTime.UtcNow.AddDays(-1);

        // Act
        var follow = Follow.Rehydrate(
            followId,
            userId,
            FollowTargetType.User,
            null,
            null,
            followedUserId,
            FollowLevel.DiscussionsAndPosts,
            createdAt);

        // Assert
        await Assert.That(follow.PublicId).IsEqualTo(followId);
        await Assert.That(follow.UserId).IsEqualTo(userId);
        await Assert.That(follow.TargetType).IsEqualTo(FollowTargetType.User);
        await Assert.That(follow.Level).IsEqualTo(FollowLevel.DiscussionsAndPosts);
        await Assert.That((object?)follow.DiscussionId).IsNull();
        await Assert.That((object?)follow.SpaceId).IsNull();
        await Assert.That(follow.FollowedUserId).IsEqualTo(followedUserId);
        await Assert.That(follow.CreatedAt).IsEqualTo(createdAt);
    }

    [Test]
    public async Task Rehydrate_DoesNotGenerateDomainEvents()
    {
        // Arrange
        var followId = FollowId.New();
        var userId = UserId.New();
        var discussionId = DiscussionId.New();

        // Act
        var follow = Follow.Rehydrate(
            followId,
            userId,
            FollowTargetType.Discussion,
            discussionId,
            null,
            null,
            FollowLevel.DiscussionsAndPosts,
            DateTime.UtcNow);

        // Assert
        await Assert.That(follow.DomainEvents).IsEmpty();
    }

    #endregion

    #region Polymorphic Behavior Tests

    [Test]
    public async Task CreateForDiscussion_AndCreateForSpace_CreateDifferentTargetTypes()
    {
        // Arrange
        var userId = UserId.New();
        var discussionId = DiscussionId.New();
        var spaceId = SpaceId.New();

        // Act
        var discussionFollow = Follow.CreateForDiscussion(userId, discussionId);
        var spaceFollow = Follow.CreateForSpace(userId, spaceId);

        // Assert
        await Assert.That(discussionFollow.TargetType).IsEqualTo(FollowTargetType.Discussion);
        await Assert.That(spaceFollow.TargetType).IsEqualTo(FollowTargetType.Space);
    }

    [Test]
    public async Task CreateForDiscussion_AndCreateForUser_HaveSameDefaultLevel()
    {
        // Arrange
        var userId = UserId.New();
        var discussionId = DiscussionId.New();
        var followedUserId = UserId.New();

        // Act
        var discussionFollow = Follow.CreateForDiscussion(userId, discussionId);
        var userFollow = Follow.CreateForUser(userId, followedUserId);

        // Assert - Both should be DiscussionsAndPosts
        await Assert.That(discussionFollow.Level).IsEqualTo(FollowLevel.DiscussionsAndPosts);
        await Assert.That(userFollow.Level).IsEqualTo(FollowLevel.DiscussionsAndPosts);
    }

    [Test]
    public async Task CreateForSpace_HasDifferentDefaultLevelThanDiscussionAndUser()
    {
        // Arrange
        var userId = UserId.New();
        var spaceId = SpaceId.New();
        var discussionId = DiscussionId.New();
        var followedUserId = UserId.New();

        // Act
        var spaceFollow = Follow.CreateForSpace(userId, spaceId);
        var discussionFollow = Follow.CreateForDiscussion(userId, discussionId);
        var userFollow = Follow.CreateForUser(userId, followedUserId);

        // Assert
        await Assert.That(spaceFollow.Level).IsEqualTo(FollowLevel.DiscussionsOnly);
        await Assert.That(discussionFollow.Level).IsEqualTo(FollowLevel.DiscussionsAndPosts);
        await Assert.That(userFollow.Level).IsEqualTo(FollowLevel.DiscussionsAndPosts);
    }

    #endregion

    #region Edge Cases

    [Test]
    public async Task CreateForDiscussion_AssignsUniqueFollowIds()
    {
        // Arrange
        var userId = UserId.New();
        var discussionId = DiscussionId.New();

        // Act
        var follow1 = Follow.CreateForDiscussion(userId, discussionId);
        var follow2 = Follow.CreateForDiscussion(userId, discussionId);

        // Assert
        await Assert.That(follow1.PublicId).IsNotEqualTo(follow2.PublicId);
    }

    [Test]
    public async Task CreateForSpace_AssignsUniqueFollowIds()
    {
        // Arrange
        var userId = UserId.New();
        var spaceId = SpaceId.New();

        // Act
        var follow1 = Follow.CreateForSpace(userId, spaceId);
        var follow2 = Follow.CreateForSpace(userId, spaceId);

        // Assert
        await Assert.That(follow1.PublicId).IsNotEqualTo(follow2.PublicId);
    }

    [Test]
    public async Task CreateForUser_AssignsUniqueFollowIds()
    {
        // Arrange
        var userId = UserId.New();
        var followedUserId = UserId.New();

        // Act
        var follow1 = Follow.CreateForUser(userId, followedUserId);
        var follow2 = Follow.CreateForUser(userId, followedUserId);

        // Assert
        await Assert.That(follow1.PublicId).IsNotEqualTo(follow2.PublicId);
    }

    [Test]
    public async Task UpdateLevel_MultipleTimesInSuccession_MaintainsCorrectState()
    {
        // Arrange
        var follow = Follow.CreateForSpace(UserId.New(), SpaceId.New());

        // Act & Assert
        follow.UpdateLevel(FollowLevel.DiscussionsAndPosts);
        await Assert.That(follow.Level).IsEqualTo(FollowLevel.DiscussionsAndPosts);

        follow.UpdateLevel(FollowLevel.DiscussionsOnly);
        await Assert.That(follow.Level).IsEqualTo(FollowLevel.DiscussionsOnly);

        follow.UpdateLevel(FollowLevel.DiscussionsAndPosts);
        await Assert.That(follow.Level).IsEqualTo(FollowLevel.DiscussionsAndPosts);

        follow.UpdateLevel(FollowLevel.DiscussionsAndPosts);
        await Assert.That(follow.Level).IsEqualTo(FollowLevel.DiscussionsAndPosts);
    }

    #endregion
}
