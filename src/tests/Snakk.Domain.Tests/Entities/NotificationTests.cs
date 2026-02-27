using Snakk.Domain.Entities;
using Snakk.Domain.Events;
using Snakk.Domain.ValueObjects;

namespace Snakk.Domain.Tests.Entities;

public class NotificationTests
{
    #region CreateForMention Tests

    [Test]
    public async Task CreateForMention_SetsTypeMention_AndCorrectTitleFormat()
    {
        // Arrange
        var recipientId = UserId.New();
        var mentionerId = UserId.New();
        var postId = PostId.New();
        var discussionId = DiscussionId.New();

        // Act
        var notification = Notification.CreateForMention(
            recipientId, mentionerId, postId, discussionId,
            "JohnDoe", "Test Discussion");

        // Assert
        await Assert.That(notification.Type).IsEqualTo(NotificationType.Mention);
        await Assert.That(notification.Title).IsEqualTo("JohnDoe mentioned you");
        await Assert.That(notification.Body).IsEqualTo("In: Test Discussion");
        await Assert.That(notification.RecipientUserId).IsEqualTo(recipientId);
        await Assert.That(notification.ActorUserId).IsEqualTo(mentionerId);
        await Assert.That(notification.SourcePostId).IsEqualTo(postId);
        await Assert.That(notification.SourceDiscussionId).IsEqualTo(discussionId);
        await Assert.That((object?)notification.SourceSpaceId).IsNull();
        await Assert.That(notification.IsRead).IsFalse();
        await Assert.That(notification.ReadAt).IsNull();
    }

    [Test]
    public async Task CreateForMention_RaisesNotificationCreatedEvent()
    {
        // Act
        var notification = Notification.CreateForMention(
            UserId.New(), UserId.New(), PostId.New(), DiscussionId.New(),
            "User", "Discussion");

        // Assert
        await Assert.That(notification.DomainEvents).Count().IsEqualTo(1);
        var domainEvent = notification.DomainEvents.First();
        await Assert.That(domainEvent).IsTypeOf<NotificationCreatedEvent>();

        var notifEvent = (NotificationCreatedEvent)domainEvent;
        await Assert.That(notifEvent.NotificationId).IsEqualTo(notification.PublicId);
        await Assert.That(notifEvent.Type).IsEqualTo(NotificationType.Mention);
    }

    #endregion

    #region CreateForReply Tests

    [Test]
    public async Task CreateForReply_SetsTypeReply()
    {
        // Act
        var notification = Notification.CreateForReply(
            UserId.New(), UserId.New(), PostId.New(), DiscussionId.New(),
            "JaneDoe", "Reply Discussion");

        // Assert
        await Assert.That(notification.Type).IsEqualTo(NotificationType.Reply);
        await Assert.That(notification.Title).IsEqualTo("JaneDoe replied to you");
        await Assert.That(notification.Body).IsEqualTo("In: Reply Discussion");
    }

    [Test]
    public async Task CreateForReply_RaisesNotificationCreatedEvent()
    {
        // Act
        var notification = Notification.CreateForReply(
            UserId.New(), UserId.New(), PostId.New(), DiscussionId.New(),
            "User", "Discussion");

        // Assert
        await Assert.That(notification.DomainEvents).Count().IsEqualTo(1);
        await Assert.That(notification.DomainEvents.First()).IsTypeOf<NotificationCreatedEvent>();
    }

    #endregion

    #region CreateForNewPost Tests

    [Test]
    public async Task CreateForNewPost_SetsTypeNewPostInFollowedDiscussion()
    {
        // Act
        var notification = Notification.CreateForNewPost(
            UserId.New(), UserId.New(), PostId.New(), DiscussionId.New(),
            "Poster", "Active Discussion");

        // Assert
        await Assert.That(notification.Type).IsEqualTo(NotificationType.NewPostInFollowedDiscussion);
        await Assert.That(notification.Title).IsEqualTo("Poster posted");
        await Assert.That(notification.Body).IsEqualTo("In: Active Discussion");
    }

    [Test]
    public async Task CreateForNewPost_RaisesNotificationCreatedEvent()
    {
        // Act
        var notification = Notification.CreateForNewPost(
            UserId.New(), UserId.New(), PostId.New(), DiscussionId.New(),
            "User", "Discussion");

        // Assert
        await Assert.That(notification.DomainEvents).Count().IsEqualTo(1);
        await Assert.That(notification.DomainEvents.First()).IsTypeOf<NotificationCreatedEvent>();
    }

    #endregion

    #region CreateForNewDiscussion Tests

    [Test]
    public async Task CreateForNewDiscussion_SetsTypeNewDiscussionInFollowedSpace_WithSpaceId()
    {
        // Arrange
        var recipientId = UserId.New();
        var authorId = UserId.New();
        var discussionId = DiscussionId.New();
        var spaceId = SpaceId.New();

        // Act
        var notification = Notification.CreateForNewDiscussion(
            recipientId, authorId, discussionId, spaceId,
            "Author", "New Topic", "General");

        // Assert
        await Assert.That(notification.Type).IsEqualTo(NotificationType.NewDiscussionInFollowedSpace);
        await Assert.That(notification.Title).IsEqualTo("Author started a discussion");
        await Assert.That(notification.Body).IsEqualTo("New Topic in General");
        await Assert.That(notification.SourceSpaceId).IsEqualTo(spaceId);
        await Assert.That(notification.SourceDiscussionId).IsEqualTo(discussionId);
        await Assert.That((object?)notification.SourcePostId).IsNull();
    }

    [Test]
    public async Task CreateForNewDiscussion_RaisesNotificationCreatedEvent()
    {
        // Act
        var notification = Notification.CreateForNewDiscussion(
            UserId.New(), UserId.New(), DiscussionId.New(), SpaceId.New(),
            "Author", "Topic", "Space");

        // Assert
        await Assert.That(notification.DomainEvents).Count().IsEqualTo(1);
        await Assert.That(notification.DomainEvents.First()).IsTypeOf<NotificationCreatedEvent>();
    }

    #endregion

    #region MarkAsRead Tests

    [Test]
    public async Task MarkAsRead_SetsIsReadAndReadAt()
    {
        // Arrange
        var notification = Notification.CreateForMention(
            UserId.New(), UserId.New(), PostId.New(), DiscussionId.New(),
            "User", "Discussion");

        // Act
        notification.MarkAsRead();

        // Assert
        await Assert.That(notification.IsRead).IsTrue();
        await Assert.That(notification.ReadAt).IsNotNull();
        await Assert.That(notification.ReadAt!.Value).IsEqualTo(DateTime.UtcNow).Within(TimeSpan.FromSeconds(1));
    }

    [Test]
    public async Task MarkAsRead_IsIdempotent_SecondCallDoesNotChangeReadAt()
    {
        // Arrange
        var notification = Notification.CreateForMention(
            UserId.New(), UserId.New(), PostId.New(), DiscussionId.New(),
            "User", "Discussion");

        // Act
        notification.MarkAsRead();
        var firstReadAt = notification.ReadAt;

        // Small delay to ensure different timestamps if it were to change
        await Task.Delay(10);
        notification.MarkAsRead();

        // Assert
        await Assert.That(notification.IsRead).IsTrue();
        await Assert.That(notification.ReadAt).IsEqualTo(firstReadAt);
    }

    #endregion

    #region Rehydrate Tests

    [Test]
    public async Task Rehydrate_RestoresAllProperties()
    {
        // Arrange
        var publicId = NotificationId.New();
        var recipientId = UserId.New();
        var actorId = UserId.New();
        var postId = PostId.New();
        var discussionId = DiscussionId.New();
        var spaceId = SpaceId.New();
        var createdAt = DateTime.UtcNow.AddHours(-5);
        var readAt = DateTime.UtcNow.AddHours(-1);

        // Act
        var notification = Notification.Rehydrate(
            publicId,
            recipientId,
            NotificationType.Reply,
            "Test Title",
            "Test Body",
            postId,
            discussionId,
            spaceId,
            actorId,
            isRead: true,
            createdAt,
            readAt);

        // Assert
        await Assert.That(notification.PublicId).IsEqualTo(publicId);
        await Assert.That(notification.RecipientUserId).IsEqualTo(recipientId);
        await Assert.That(notification.Type).IsEqualTo(NotificationType.Reply);
        await Assert.That(notification.Title).IsEqualTo("Test Title");
        await Assert.That(notification.Body).IsEqualTo("Test Body");
        await Assert.That(notification.SourcePostId).IsEqualTo(postId);
        await Assert.That(notification.SourceDiscussionId).IsEqualTo(discussionId);
        await Assert.That(notification.SourceSpaceId).IsEqualTo(spaceId);
        await Assert.That(notification.ActorUserId).IsEqualTo(actorId);
        await Assert.That(notification.IsRead).IsTrue();
        await Assert.That(notification.CreatedAt).IsEqualTo(createdAt);
        await Assert.That(notification.ReadAt).IsEqualTo(readAt);
    }

    [Test]
    public async Task Rehydrate_DoesNotRaiseEvents()
    {
        // Act
        var notification = Notification.Rehydrate(
            NotificationId.New(), UserId.New(), NotificationType.Mention,
            "Title", null, null, null, null, null, false, DateTime.UtcNow, null);

        // Assert
        await Assert.That(notification.DomainEvents).IsEmpty();
    }

    #endregion
}
