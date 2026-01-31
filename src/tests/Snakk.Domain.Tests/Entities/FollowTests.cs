using FluentAssertions;
using Snakk.Domain.Entities;
using Snakk.Domain.Events;
using Snakk.Domain.ValueObjects;

namespace Snakk.Domain.Tests.Entities;

public class FollowTests
{
    #region CreateForDiscussion Tests

    [Fact]
    public void CreateForDiscussion_WithValidParameters_CreatesFollowWithDiscussionsAndPostsLevel()
    {
        // Arrange
        var userId = UserId.New();
        var discussionId = DiscussionId.New();

        // Act
        var follow = Follow.CreateForDiscussion(userId, discussionId);

        // Assert
        follow.Should().NotBeNull();
        follow.PublicId.Should().NotBe(Guid.Empty);
        follow.UserId.Should().Be(userId);
        follow.TargetType.Should().Be(FollowTargetType.Discussion);
        follow.DiscussionId.Should().Be(discussionId);
        follow.SpaceId.Should().BeNull();
        follow.FollowedUserId.Should().BeNull();
        follow.Level.Should().Be(FollowLevel.DiscussionsAndPosts); // Always DiscussionsAndPosts for discussions
        follow.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void CreateForDiscussion_AlwaysUsesDiscussionsAndPostsLevel()
    {
        // Arrange
        var userId = UserId.New();
        var discussionId = DiscussionId.New();

        // Act
        var follow = Follow.CreateForDiscussion(userId, discussionId);

        // Assert
        follow.Level.Should().Be(FollowLevel.DiscussionsAndPosts);
    }

    [Fact]
    public void CreateForDiscussion_SetsCorrectTargetType()
    {
        // Arrange
        var userId = UserId.New();
        var discussionId = DiscussionId.New();

        // Act
        var follow = Follow.CreateForDiscussion(userId, discussionId);

        // Assert
        follow.TargetType.Should().Be(FollowTargetType.Discussion);
    }

    #endregion

    #region CreateForSpace Tests

    [Fact]
    public void CreateForSpace_WithDefaultLevel_CreatesFollowWithDiscussionsOnlyLevel()
    {
        // Arrange
        var userId = UserId.New();
        var spaceId = SpaceId.New();

        // Act
        var follow = Follow.CreateForSpace(userId, spaceId);

        // Assert
        follow.Should().NotBeNull();
        follow.PublicId.Should().NotBe(Guid.Empty);
        follow.UserId.Should().Be(userId);
        follow.TargetType.Should().Be(FollowTargetType.Space);
        follow.SpaceId.Should().Be(spaceId);
        follow.DiscussionId.Should().BeNull();
        follow.FollowedUserId.Should().BeNull();
        follow.Level.Should().Be(FollowLevel.DiscussionsOnly); // Default for spaces
        follow.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void CreateForSpace_WithDiscussionsOnlyLevel_CreatesFollowWithDiscussionsOnlyLevel()
    {
        // Arrange
        var userId = UserId.New();
        var spaceId = SpaceId.New();

        // Act
        var follow = Follow.CreateForSpace(userId, spaceId, FollowLevel.DiscussionsOnly);

        // Assert
        follow.Level.Should().Be(FollowLevel.DiscussionsOnly);
    }

    [Fact]
    public void CreateForSpace_WithDiscussionsAndPostsLevel_CreatesFollowWithDiscussionsAndPostsLevel()
    {
        // Arrange
        var userId = UserId.New();
        var spaceId = SpaceId.New();

        // Act
        var follow = Follow.CreateForSpace(userId, spaceId, FollowLevel.DiscussionsAndPosts);

        // Assert
        follow.Level.Should().Be(FollowLevel.DiscussionsAndPosts);
    }

    [Fact]
    public void CreateForSpace_SetsCorrectTargetType()
    {
        // Arrange
        var userId = UserId.New();
        var spaceId = SpaceId.New();

        // Act
        var follow = Follow.CreateForSpace(userId, spaceId);

        // Assert
        follow.TargetType.Should().Be(FollowTargetType.Space);
    }

    #endregion

    #region CreateForUser Tests

    [Fact]
    public void CreateForUser_WithValidParameters_CreatesFollowWithDiscussionsAndPostsLevel()
    {
        // Arrange
        var userId = UserId.New();
        var followedUserId = UserId.New();

        // Act
        var follow = Follow.CreateForUser(userId, followedUserId);

        // Assert
        follow.Should().NotBeNull();
        follow.PublicId.Should().NotBe(Guid.Empty);
        follow.UserId.Should().Be(userId);
        follow.TargetType.Should().Be(FollowTargetType.User);
        follow.FollowedUserId.Should().Be(followedUserId);
        follow.DiscussionId.Should().BeNull();
        follow.SpaceId.Should().BeNull();
        follow.Level.Should().Be(FollowLevel.DiscussionsAndPosts); // Always DiscussionsAndPosts for users
        follow.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void CreateForUser_AlwaysUsesDiscussionsAndPostsLevel()
    {
        // Arrange
        var userId = UserId.New();
        var followedUserId = UserId.New();

        // Act
        var follow = Follow.CreateForUser(userId, followedUserId);

        // Assert
        follow.Level.Should().Be(FollowLevel.DiscussionsAndPosts);
    }

    [Fact]
    public void CreateForUser_SetsCorrectTargetType()
    {
        // Arrange
        var userId = UserId.New();
        var followedUserId = UserId.New();

        // Act
        var follow = Follow.CreateForUser(userId, followedUserId);

        // Assert
        follow.TargetType.Should().Be(FollowTargetType.User);
    }

    #endregion

    #region UpdateLevel Tests

    [Fact]
    public void UpdateLevel_OnSpaceFollow_UpdatesLevel()
    {
        // Arrange
        var follow = Follow.CreateForSpace(UserId.New(), SpaceId.New(), FollowLevel.DiscussionsOnly);

        // Act
        follow.UpdateLevel(FollowLevel.DiscussionsAndPosts);

        // Assert
        follow.Level.Should().Be(FollowLevel.DiscussionsAndPosts);
    }

    [Fact]
    public void UpdateLevel_CanToggleBetweenLevels()
    {
        // Arrange
        var follow = Follow.CreateForSpace(UserId.New(), SpaceId.New(), FollowLevel.DiscussionsOnly);

        // Act & Assert
        follow.Level.Should().Be(FollowLevel.DiscussionsOnly);

        follow.UpdateLevel(FollowLevel.DiscussionsAndPosts);
        follow.Level.Should().Be(FollowLevel.DiscussionsAndPosts);

        follow.UpdateLevel(FollowLevel.DiscussionsOnly);
        follow.Level.Should().Be(FollowLevel.DiscussionsOnly);
    }

    [Fact]
    public void UpdateLevel_WithSameLevel_DoesNotThrow()
    {
        // Arrange
        var follow = Follow.CreateForSpace(UserId.New(), SpaceId.New(), FollowLevel.DiscussionsOnly);

        // Act
        var act = () => follow.UpdateLevel(FollowLevel.DiscussionsOnly);

        // Assert
        act.Should().NotThrow();
        follow.Level.Should().Be(FollowLevel.DiscussionsOnly);
    }

    #endregion

    #region MarkForRemoval Tests

    [Fact]
    public void MarkForRemoval_OnDiscussionFollow_GeneratesFollowRemovedEventWithDiscussionId()
    {
        // Arrange
        var userId = UserId.New();
        var discussionId = DiscussionId.New();
        var follow = Follow.CreateForDiscussion(userId, discussionId);
        follow.ClearDomainEvents();

        // Act
        follow.MarkForRemoval();

        // Assert
        follow.DomainEvents.Should().ContainSingle();
        var removedEvent = follow.DomainEvents.First() as FollowRemovedEvent;
        removedEvent.Should().NotBeNull();
        removedEvent!.UserId.Should().Be(userId);
        removedEvent.TargetId.Should().Be(discussionId.Value);
        removedEvent.TargetType.Should().Be(FollowTargetType.Discussion);
    }

    [Fact]
    public void MarkForRemoval_OnSpaceFollow_GeneratesFollowRemovedEventWithSpaceId()
    {
        // Arrange
        var userId = UserId.New();
        var spaceId = SpaceId.New();
        var follow = Follow.CreateForSpace(userId, spaceId);
        follow.ClearDomainEvents();

        // Act
        follow.MarkForRemoval();

        // Assert
        follow.DomainEvents.Should().ContainSingle();
        var removedEvent = follow.DomainEvents.First() as FollowRemovedEvent;
        removedEvent.Should().NotBeNull();
        removedEvent!.UserId.Should().Be(userId);
        removedEvent.TargetId.Should().Be(spaceId.Value);
        removedEvent.TargetType.Should().Be(FollowTargetType.Space);
    }

    [Fact]
    public void MarkForRemoval_OnUserFollow_GeneratesFollowRemovedEventWithFollowedUserId()
    {
        // Arrange
        var userId = UserId.New();
        var followedUserId = UserId.New();
        var follow = Follow.CreateForUser(userId, followedUserId);
        follow.ClearDomainEvents();

        // Act
        follow.MarkForRemoval();

        // Assert
        follow.DomainEvents.Should().ContainSingle();
        var removedEvent = follow.DomainEvents.First() as FollowRemovedEvent;
        removedEvent.Should().NotBeNull();
        removedEvent!.UserId.Should().Be(userId);
        removedEvent.TargetId.Should().Be(followedUserId.Value);
        removedEvent.TargetType.Should().Be(FollowTargetType.User);
    }

    #endregion

    #region Rehydrate Tests

    [Fact]
    public void Rehydrate_ForDiscussion_RestoresFollow()
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
        follow.PublicId.Should().Be(followId);
        follow.UserId.Should().Be(userId);
        follow.TargetType.Should().Be(FollowTargetType.Discussion);
        follow.Level.Should().Be(FollowLevel.DiscussionsAndPosts);
        follow.DiscussionId.Should().Be(discussionId);
        follow.SpaceId.Should().BeNull();
        follow.FollowedUserId.Should().BeNull();
        follow.CreatedAt.Should().Be(createdAt);
    }

    [Fact]
    public void Rehydrate_ForSpace_RestoresFollow()
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
        follow.PublicId.Should().Be(followId);
        follow.UserId.Should().Be(userId);
        follow.TargetType.Should().Be(FollowTargetType.Space);
        follow.Level.Should().Be(FollowLevel.DiscussionsOnly);
        follow.DiscussionId.Should().BeNull();
        follow.SpaceId.Should().Be(spaceId);
        follow.FollowedUserId.Should().BeNull();
        follow.CreatedAt.Should().Be(createdAt);
    }

    [Fact]
    public void Rehydrate_ForUser_RestoresFollow()
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
        follow.PublicId.Should().Be(followId);
        follow.UserId.Should().Be(userId);
        follow.TargetType.Should().Be(FollowTargetType.User);
        follow.Level.Should().Be(FollowLevel.DiscussionsAndPosts);
        follow.DiscussionId.Should().BeNull();
        follow.SpaceId.Should().BeNull();
        follow.FollowedUserId.Should().Be(followedUserId);
        follow.CreatedAt.Should().Be(createdAt);
    }

    [Fact]
    public void Rehydrate_DoesNotGenerateDomainEvents()
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
        follow.DomainEvents.Should().BeEmpty();
    }

    #endregion

    #region Polymorphic Behavior Tests

    [Fact]
    public void CreateForDiscussion_AndCreateForSpace_CreateDifferentTargetTypes()
    {
        // Arrange
        var userId = UserId.New();
        var discussionId = DiscussionId.New();
        var spaceId = SpaceId.New();

        // Act
        var discussionFollow = Follow.CreateForDiscussion(userId, discussionId);
        var spaceFollow = Follow.CreateForSpace(userId, spaceId);

        // Assert
        discussionFollow.TargetType.Should().Be(FollowTargetType.Discussion);
        spaceFollow.TargetType.Should().Be(FollowTargetType.Space);
    }

    [Fact]
    public void CreateForDiscussion_AndCreateForUser_HaveSameDefaultLevel()
    {
        // Arrange
        var userId = UserId.New();
        var discussionId = DiscussionId.New();
        var followedUserId = UserId.New();

        // Act
        var discussionFollow = Follow.CreateForDiscussion(userId, discussionId);
        var userFollow = Follow.CreateForUser(userId, followedUserId);

        // Assert - Both should be DiscussionsAndPosts
        discussionFollow.Level.Should().Be(FollowLevel.DiscussionsAndPosts);
        userFollow.Level.Should().Be(FollowLevel.DiscussionsAndPosts);
    }

    [Fact]
    public void CreateForSpace_HasDifferentDefaultLevelThanDiscussionAndUser()
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
        spaceFollow.Level.Should().Be(FollowLevel.DiscussionsOnly);
        discussionFollow.Level.Should().Be(FollowLevel.DiscussionsAndPosts);
        userFollow.Level.Should().Be(FollowLevel.DiscussionsAndPosts);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void CreateForDiscussion_AssignsUniqueFollowIds()
    {
        // Arrange
        var userId = UserId.New();
        var discussionId = DiscussionId.New();

        // Act
        var follow1 = Follow.CreateForDiscussion(userId, discussionId);
        var follow2 = Follow.CreateForDiscussion(userId, discussionId);

        // Assert
        follow1.PublicId.Should().NotBe(follow2.PublicId);
    }

    [Fact]
    public void CreateForSpace_AssignsUniqueFollowIds()
    {
        // Arrange
        var userId = UserId.New();
        var spaceId = SpaceId.New();

        // Act
        var follow1 = Follow.CreateForSpace(userId, spaceId);
        var follow2 = Follow.CreateForSpace(userId, spaceId);

        // Assert
        follow1.PublicId.Should().NotBe(follow2.PublicId);
    }

    [Fact]
    public void CreateForUser_AssignsUniqueFollowIds()
    {
        // Arrange
        var userId = UserId.New();
        var followedUserId = UserId.New();

        // Act
        var follow1 = Follow.CreateForUser(userId, followedUserId);
        var follow2 = Follow.CreateForUser(userId, followedUserId);

        // Assert
        follow1.PublicId.Should().NotBe(follow2.PublicId);
    }

    [Fact]
    public void UpdateLevel_MultipleTimesInSuccession_MaintainsCorrectState()
    {
        // Arrange
        var follow = Follow.CreateForSpace(UserId.New(), SpaceId.New());

        // Act & Assert
        follow.UpdateLevel(FollowLevel.DiscussionsAndPosts);
        follow.Level.Should().Be(FollowLevel.DiscussionsAndPosts);

        follow.UpdateLevel(FollowLevel.DiscussionsOnly);
        follow.Level.Should().Be(FollowLevel.DiscussionsOnly);

        follow.UpdateLevel(FollowLevel.DiscussionsAndPosts);
        follow.Level.Should().Be(FollowLevel.DiscussionsAndPosts);

        follow.UpdateLevel(FollowLevel.DiscussionsAndPosts);
        follow.Level.Should().Be(FollowLevel.DiscussionsAndPosts);
    }

    #endregion
}
