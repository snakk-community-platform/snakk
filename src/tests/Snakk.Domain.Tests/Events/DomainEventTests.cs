using Snakk.Domain.Events;
using Snakk.Domain.ValueObjects;

namespace Snakk.Domain.Tests.Events;

public class DomainEventTests
{
    #region PostCreatedEvent Tests

    [Test]
    public async Task PostCreatedEvent_HasCorrectOccurredAt()
    {
        // Act
        var evt = new PostCreatedEvent(PostId.New(), DiscussionId.New(), UserId.New());

        // Assert
        await Assert.That(evt.OccurredAt).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task PostCreatedEvent_StoresPropertiesCorrectly()
    {
        // Arrange
        var postId = PostId.New();
        var discussionId = DiscussionId.New();
        var userId = UserId.New();

        // Act
        var evt = new PostCreatedEvent(postId, discussionId, userId);

        // Assert
        await Assert.That(evt.PostId).IsEqualTo(postId);
        await Assert.That(evt.DiscussionId).IsEqualTo(discussionId);
        await Assert.That(evt.CreatedByUserId).IsEqualTo(userId);
    }

    #endregion

    #region PostEditedEvent Tests

    [Test]
    public async Task PostEditedEvent_HasCorrectOccurredAt()
    {
        var evt = new PostEditedEvent(PostId.New(), DiscussionId.New());
        await Assert.That(evt.OccurredAt).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task PostEditedEvent_StoresPropertiesCorrectly()
    {
        var postId = PostId.New();
        var discussionId = DiscussionId.New();

        var evt = new PostEditedEvent(postId, discussionId);

        await Assert.That(evt.PostId).IsEqualTo(postId);
        await Assert.That(evt.DiscussionId).IsEqualTo(discussionId);
    }

    #endregion

    #region PostDeletedEvent Tests

    [Test]
    public async Task PostDeletedEvent_HasCorrectOccurredAt()
    {
        var evt = new PostDeletedEvent(PostId.New(), DiscussionId.New(), UserId.New(), false);
        await Assert.That(evt.OccurredAt).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task PostDeletedEvent_StoresPropertiesCorrectly()
    {
        var postId = PostId.New();
        var discussionId = DiscussionId.New();
        var userId = UserId.New();

        var evt = new PostDeletedEvent(postId, discussionId, userId, true);

        await Assert.That(evt.PostId).IsEqualTo(postId);
        await Assert.That(evt.DiscussionId).IsEqualTo(discussionId);
        await Assert.That(evt.DeletedByUserId).IsEqualTo(userId);
        await Assert.That(evt.IsHardDelete).IsTrue();
    }

    #endregion

    #region DiscussionCreatedEvent Tests

    [Test]
    public async Task DiscussionCreatedEvent_HasCorrectOccurredAt()
    {
        var evt = new DiscussionCreatedEvent(DiscussionId.New(), SpaceId.New(), UserId.New());
        await Assert.That(evt.OccurredAt).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task DiscussionCreatedEvent_StoresPropertiesCorrectly()
    {
        var discussionId = DiscussionId.New();
        var spaceId = SpaceId.New();
        var userId = UserId.New();

        var evt = new DiscussionCreatedEvent(discussionId, spaceId, userId);

        await Assert.That(evt.DiscussionId).IsEqualTo(discussionId);
        await Assert.That(evt.SpaceId).IsEqualTo(spaceId);
        await Assert.That(evt.CreatedByUserId).IsEqualTo(userId);
    }

    #endregion

    #region ReactionAddedEvent Tests

    [Test]
    public async Task ReactionAddedEvent_HasCorrectOccurredAt()
    {
        var evt = new ReactionAddedEvent(ReactionId.New(), PostId.New(), UserId.New(), ReactionType.Love);
        await Assert.That(evt.OccurredAt).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task ReactionAddedEvent_StoresPropertiesCorrectly()
    {
        var reactionId = ReactionId.New();
        var postId = PostId.New();
        var userId = UserId.New();

        var evt = new ReactionAddedEvent(reactionId, postId, userId, ReactionType.Agree);

        await Assert.That(evt.ReactionId).IsEqualTo(reactionId);
        await Assert.That(evt.PostId).IsEqualTo(postId);
        await Assert.That(evt.UserId).IsEqualTo(userId);
        await Assert.That(evt.Type).IsEqualTo(ReactionType.Agree);
    }

    #endregion

    #region ReactionRemovedEvent Tests

    [Test]
    public async Task ReactionRemovedEvent_HasCorrectOccurredAt()
    {
        var evt = new ReactionRemovedEvent(ReactionId.New(), PostId.New(), UserId.New(), ReactionType.Watching);
        await Assert.That(evt.OccurredAt).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task ReactionRemovedEvent_StoresPropertiesCorrectly()
    {
        var reactionId = ReactionId.New();
        var postId = PostId.New();
        var userId = UserId.New();

        var evt = new ReactionRemovedEvent(reactionId, postId, userId, ReactionType.MindBlown);

        await Assert.That(evt.ReactionId).IsEqualTo(reactionId);
        await Assert.That(evt.PostId).IsEqualTo(postId);
        await Assert.That(evt.UserId).IsEqualTo(userId);
        await Assert.That(evt.Type).IsEqualTo(ReactionType.MindBlown);
    }

    #endregion

    #region FollowCreatedEvent Tests

    [Test]
    public async Task FollowCreatedEvent_HasCorrectOccurredAt()
    {
        var evt = new FollowCreatedEvent(FollowId.New(), UserId.New(), FollowTargetType.Discussion, "target-id");
        await Assert.That(evt.OccurredAt).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task FollowCreatedEvent_StoresPropertiesCorrectly()
    {
        var followId = FollowId.New();
        var userId = UserId.New();

        var evt = new FollowCreatedEvent(followId, userId, FollowTargetType.User, "target-123");

        await Assert.That(evt.FollowId).IsEqualTo(followId);
        await Assert.That(evt.UserId).IsEqualTo(userId);
        await Assert.That(evt.TargetType).IsEqualTo(FollowTargetType.User);
        await Assert.That(evt.TargetId).IsEqualTo("target-123");
    }

    #endregion

    #region FollowRemovedEvent Tests

    [Test]
    public async Task FollowRemovedEvent_HasCorrectOccurredAt()
    {
        var evt = new FollowRemovedEvent(FollowId.New(), UserId.New(), FollowTargetType.Space, "target-id");
        await Assert.That(evt.OccurredAt).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task FollowRemovedEvent_StoresPropertiesCorrectly()
    {
        var followId = FollowId.New();
        var userId = UserId.New();

        var evt = new FollowRemovedEvent(followId, userId, FollowTargetType.Space, "space-target");

        await Assert.That(evt.FollowId).IsEqualTo(followId);
        await Assert.That(evt.UserId).IsEqualTo(userId);
        await Assert.That(evt.TargetType).IsEqualTo(FollowTargetType.Space);
        await Assert.That(evt.TargetId).IsEqualTo("space-target");
    }

    #endregion

    #region MentionCreatedEvent Tests

    [Test]
    public async Task MentionCreatedEvent_HasCorrectOccurredAt()
    {
        var evt = new MentionCreatedEvent(MentionId.New(), PostId.New(), UserId.New());
        await Assert.That(evt.OccurredAt).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task MentionCreatedEvent_StoresPropertiesCorrectly()
    {
        var mentionId = MentionId.New();
        var postId = PostId.New();
        var userId = UserId.New();

        var evt = new MentionCreatedEvent(mentionId, postId, userId);

        await Assert.That(evt.MentionId).IsEqualTo(mentionId);
        await Assert.That(evt.PostId).IsEqualTo(postId);
        await Assert.That(evt.MentionedUserId).IsEqualTo(userId);
    }

    #endregion

    #region NotificationCreatedEvent Tests

    [Test]
    public async Task NotificationCreatedEvent_HasCorrectOccurredAt()
    {
        var evt = new NotificationCreatedEvent(NotificationId.New(), UserId.New(), NotificationType.Mention);
        await Assert.That(evt.OccurredAt).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task NotificationCreatedEvent_StoresPropertiesCorrectly()
    {
        var notificationId = NotificationId.New();
        var userId = UserId.New();

        var evt = new NotificationCreatedEvent(notificationId, userId, NotificationType.Reply);

        await Assert.That(evt.NotificationId).IsEqualTo(notificationId);
        await Assert.That(evt.RecipientUserId).IsEqualTo(userId);
        await Assert.That(evt.Type).IsEqualTo(NotificationType.Reply);
    }

    #endregion

    #region UserCreatedEvent Tests

    [Test]
    public async Task UserCreatedEvent_HasCorrectOccurredAt()
    {
        var evt = new UserCreatedEvent(UserId.New());
        await Assert.That(evt.OccurredAt).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task UserCreatedEvent_StoresPropertiesCorrectly()
    {
        var userId = UserId.New();
        var evt = new UserCreatedEvent(userId);
        await Assert.That(evt.UserId).IsEqualTo(userId);
    }

    #endregion

    #region UserDeletedEvent Tests

    [Test]
    public async Task UserDeletedEvent_HasCorrectOccurredAt()
    {
        var evt = new UserDeletedEvent(UserId.New());
        await Assert.That(evt.OccurredAt).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task UserDeletedEvent_StoresPropertiesCorrectly()
    {
        var userId = UserId.New();
        var evt = new UserDeletedEvent(userId);
        await Assert.That(evt.UserId).IsEqualTo(userId);
    }

    #endregion

    #region HubCreatedEvent Tests

    [Test]
    public async Task HubCreatedEvent_HasCorrectOccurredAt()
    {
        var evt = new HubCreatedEvent(HubId.New());
        await Assert.That(evt.OccurredAt).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task HubCreatedEvent_StoresPropertiesCorrectly()
    {
        var hubId = HubId.New();
        var evt = new HubCreatedEvent(hubId);
        await Assert.That(evt.HubId).IsEqualTo(hubId);
    }

    #endregion

    #region HubDeletedEvent Tests

    [Test]
    public async Task HubDeletedEvent_HasCorrectOccurredAt()
    {
        var evt = new HubDeletedEvent(HubId.New());
        await Assert.That(evt.OccurredAt).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task HubDeletedEvent_StoresPropertiesCorrectly()
    {
        var hubId = HubId.New();
        var evt = new HubDeletedEvent(hubId);
        await Assert.That(evt.HubId).IsEqualTo(hubId);
    }

    #endregion

    #region SpaceCreatedEvent Tests

    [Test]
    public async Task SpaceCreatedEvent_HasCorrectOccurredAt()
    {
        var evt = new SpaceCreatedEvent(SpaceId.New());
        await Assert.That(evt.OccurredAt).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task SpaceCreatedEvent_StoresPropertiesCorrectly()
    {
        var spaceId = SpaceId.New();
        var evt = new SpaceCreatedEvent(spaceId);
        await Assert.That(evt.SpaceId).IsEqualTo(spaceId);
    }

    #endregion

    #region SpaceDeletedEvent Tests

    [Test]
    public async Task SpaceDeletedEvent_HasCorrectOccurredAt()
    {
        var evt = new SpaceDeletedEvent(SpaceId.New());
        await Assert.That(evt.OccurredAt).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task SpaceDeletedEvent_StoresPropertiesCorrectly()
    {
        var spaceId = SpaceId.New();
        var evt = new SpaceDeletedEvent(spaceId);
        await Assert.That(evt.SpaceId).IsEqualTo(spaceId);
    }

    #endregion

    #region CommunityCreatedEvent Tests

    [Test]
    public async Task CommunityCreatedEvent_HasCorrectOccurredAt()
    {
        var evt = new CommunityCreatedEvent(CommunityId.New());
        await Assert.That(evt.OccurredAt).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task CommunityCreatedEvent_StoresPropertiesCorrectly()
    {
        var communityId = CommunityId.New();
        var evt = new CommunityCreatedEvent(communityId);
        await Assert.That(evt.CommunityId).IsEqualTo(communityId);
    }

    #endregion

    #region CommunityDeletedEvent Tests

    [Test]
    public async Task CommunityDeletedEvent_HasCorrectOccurredAt()
    {
        var evt = new CommunityDeletedEvent(CommunityId.New());
        await Assert.That(evt.OccurredAt).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task CommunityDeletedEvent_StoresPropertiesCorrectly()
    {
        var communityId = CommunityId.New();
        var evt = new CommunityDeletedEvent(communityId);
        await Assert.That(evt.CommunityId).IsEqualTo(communityId);
    }

    #endregion
}
