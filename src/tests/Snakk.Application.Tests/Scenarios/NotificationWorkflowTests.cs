using Moq;
using Snakk.Application.Services;
using Snakk.Application.UseCases;
using Snakk.Domain.Entities;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Models;

namespace Snakk.Application.Tests.Scenarios;

/// <summary>
/// Comprehensive workflow tests for notification scenarios
/// </summary>
public class NotificationWorkflowTests
{
    private readonly Mock<INotificationRepository> _mockNotificationRepository = new();
    private readonly Mock<IRealtimeNotifier> _mockRealtimeNotifier = new();
    private NotificationUseCase _useCase = null!;

    [Before(Test)]
    public void Setup()
    {
        _useCase = new NotificationUseCase(
            _mockNotificationRepository.Object,
            _mockRealtimeNotifier.Object);
    }

    #region User Follows Discussion -> New Post -> Notification Created

    [Test]
    public async Task Workflow_FollowedDiscussion_NewPost_NotificationCreated()
    {
        // Arrange - simulate notification for a new post in a followed discussion
        var recipientUserId = UserId.New();
        var posterUserId = UserId.New();
        var postId = PostId.New();
        var discussionId = DiscussionId.New();

        var notification = Notification.CreateForNewPost(
            recipientUserId, posterUserId, postId, discussionId,
            "PosterUser", "Interesting Discussion");

        // Act - notification is created and persisted
        await _useCase.CreateNotificationAsync(notification);

        // Assert - notification was persisted and real-time delivery happened
        _mockNotificationRepository.Verify(r => r.AddAsync(notification), Times.Once);
        _mockRealtimeNotifier.Verify(r => r.NotifyUserAsync(recipientUserId, It.IsAny<object>()), Times.Once);

        // Verify notification properties
        await Assert.That(notification.Type).IsEqualTo(NotificationType.NewPostInFollowedDiscussion);
        await Assert.That(notification.RecipientUserId).IsEqualTo(recipientUserId);
        await Assert.That(notification.SourcePostId).IsEqualTo(postId);
        await Assert.That(notification.SourceDiscussionId).IsEqualTo(discussionId);
        await Assert.That(notification.ActorUserId).IsEqualTo(posterUserId);
        await Assert.That(notification.IsRead).IsFalse();
    }

    [Test]
    public async Task Workflow_FollowedDiscussion_NewPost_UnreadCountIncrements()
    {
        // Arrange
        var userId = UserId.New();

        _mockNotificationRepository.Setup(r => r.GetUnreadCountAsync(userId))
            .ReturnsAsync(5);

        // Act
        var unreadCount = await _useCase.GetUnreadCountAsync(userId);

        // Assert
        await Assert.That(unreadCount).IsEqualTo(5);
    }

    #endregion

    #region User Mentioned in Post -> Notification Created

    [Test]
    public async Task Workflow_UserMentionedInPost_NotificationCreated()
    {
        // Arrange
        var recipientUserId = UserId.New();
        var mentionerUserId = UserId.New();
        var postId = PostId.New();
        var discussionId = DiscussionId.New();

        var notification = Notification.CreateForMention(
            recipientUserId, mentionerUserId, postId, discussionId,
            "MentionerUser", "Some Discussion Title");

        // Act
        await _useCase.CreateNotificationAsync(notification);

        // Assert
        _mockNotificationRepository.Verify(r => r.AddAsync(notification), Times.Once);
        _mockRealtimeNotifier.Verify(r => r.NotifyUserAsync(recipientUserId, It.IsAny<object>()), Times.Once);

        await Assert.That(notification.Type).IsEqualTo(NotificationType.Mention);
        await Assert.That(notification.Title).Contains("MentionerUser");
        await Assert.That(notification.Title).Contains("mentioned you");
    }

    [Test]
    public async Task Workflow_UserMentionedInPost_CanReadNotification()
    {
        // Arrange - create and persist a mention notification
        var recipientUserId = UserId.New();
        var notification = Notification.CreateForMention(
            recipientUserId, UserId.New(), PostId.New(), DiscussionId.New(),
            "Mentioner", "Discussion Title");

        var pagedResult = new PagedResult<Notification>
        {
            Items = new List<Notification> { notification },
            Offset = 0,
            PageSize = 20,
            HasMoreItems = false
        };

        _mockNotificationRepository.Setup(r => r.GetByUserIdAsync(recipientUserId, 0, 20))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _useCase.GetNotificationsAsync(recipientUserId, 0, 20);

        // Assert
        await Assert.That(result.Items).Count().IsEqualTo(1);
        var dto = result.Items.First();
        await Assert.That(dto.Type).IsEqualTo("Mention");
        await Assert.That(dto.IsRead).IsFalse();
    }

    #endregion

    #region User Reads All Notifications -> Unread Count Is 0

    [Test]
    public async Task Workflow_UserReadsAllNotifications_UnreadCountIsZero()
    {
        // Arrange
        var userId = UserId.New();

        // Step 1: User has unread notifications
        _mockNotificationRepository.Setup(r => r.GetUnreadCountAsync(userId))
            .ReturnsAsync(7);

        var unreadBefore = await _useCase.GetUnreadCountAsync(userId);
        await Assert.That(unreadBefore).IsEqualTo(7);

        // Step 2: User marks all as read
        await _useCase.MarkAllAsReadAsync(userId);

        // Assert
        _mockNotificationRepository.Verify(r => r.MarkAllAsReadAsync(userId), Times.Once);
        _mockRealtimeNotifier.Verify(r => r.NotifyUnreadCountUpdatedAsync(userId, 0), Times.Once);
    }

    [Test]
    public async Task Workflow_UserMarksSingleNotificationAsRead_UpdatesCount()
    {
        // Arrange
        var userId = UserId.New();
        var notification = Notification.CreateForReply(
            userId, UserId.New(), PostId.New(), DiscussionId.New(),
            "Replier", "Discussion");

        _mockNotificationRepository.Setup(r => r.GetByPublicIdAsync(notification.PublicId))
            .ReturnsAsync(notification);
        _mockNotificationRepository.Setup(r => r.GetUnreadCountAsync(userId))
            .ReturnsAsync(2);

        // Act
        var result = await _useCase.MarkAsReadAsync(notification.PublicId, userId);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(notification.IsRead).IsTrue();
        _mockNotificationRepository.Verify(r => r.UpdateAsync(notification), Times.Once);
        _mockRealtimeNotifier.Verify(r => r.NotifyUnreadCountUpdatedAsync(userId, 2), Times.Once);
    }

    [Test]
    public async Task Workflow_UserCannotMarkOtherUsersNotification()
    {
        // Arrange
        var ownerUserId = UserId.New();
        var otherUserId = UserId.New();
        var notification = Notification.CreateForMention(
            ownerUserId, UserId.New(), PostId.New(), DiscussionId.New(),
            "Mentioner", "Discussion");

        _mockNotificationRepository.Setup(r => r.GetByPublicIdAsync(notification.PublicId))
            .ReturnsAsync(notification);

        // Act
        var result = await _useCase.MarkAsReadAsync(notification.PublicId, otherUserId);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Cannot mark other user's notification");
        await Assert.That(notification.IsRead).IsFalse();
    }

    #endregion

    #region Multiple Notification Types

    [Test]
    public async Task Workflow_MultipleNotificationTypes_AllDelivered()
    {
        // Arrange
        var recipientUserId = UserId.New();
        var actorUserId = UserId.New();
        var postId = PostId.New();
        var discussionId = DiscussionId.New();
        var spaceId = SpaceId.New();

        var mention = Notification.CreateForMention(
            recipientUserId, actorUserId, postId, discussionId, "User1", "Discussion 1");
        var reply = Notification.CreateForReply(
            recipientUserId, actorUserId, postId, discussionId, "User1", "Discussion 1");
        var newPost = Notification.CreateForNewPost(
            recipientUserId, actorUserId, postId, discussionId, "User1", "Discussion 1");
        var newDiscussion = Notification.CreateForNewDiscussion(
            recipientUserId, actorUserId, discussionId, spaceId, "User1", "New Discussion", "Space Name");

        // Act
        await _useCase.CreateNotificationAsync(mention);
        await _useCase.CreateNotificationAsync(reply);
        await _useCase.CreateNotificationAsync(newPost);
        await _useCase.CreateNotificationAsync(newDiscussion);

        // Assert
        _mockNotificationRepository.Verify(r => r.AddAsync(It.IsAny<Notification>()), Times.Exactly(4));
        _mockRealtimeNotifier.Verify(r => r.NotifyUserAsync(recipientUserId, It.IsAny<object>()), Times.Exactly(4));
    }

    [Test]
    public async Task Workflow_GetNotifications_MapsAllTypes()
    {
        // Arrange
        var userId = UserId.New();
        var actorId = UserId.New();
        var postId = PostId.New();
        var discussionId = DiscussionId.New();

        var notifications = new List<Notification>
        {
            Notification.CreateForMention(userId, actorId, postId, discussionId, "User1", "Discussion"),
            Notification.CreateForReply(userId, actorId, postId, discussionId, "User1", "Discussion"),
            Notification.CreateForNewPost(userId, actorId, postId, discussionId, "User1", "Discussion")
        };

        var pagedResult = new PagedResult<Notification>
        {
            Items = notifications,
            Offset = 0,
            PageSize = 20,
            HasMoreItems = false
        };

        _mockNotificationRepository.Setup(r => r.GetByUserIdAsync(userId, 0, 20))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _useCase.GetNotificationsAsync(userId, 0, 20);

        // Assert
        await Assert.That(result.Items).Count().IsEqualTo(3);
        var types = result.Items.Select(n => n.Type).ToList();
        await Assert.That(types).Contains("Mention");
        await Assert.That(types).Contains("Reply");
        await Assert.That(types).Contains("NewPostInFollowedDiscussion");
    }

    #endregion

    #region Mark As Read Non-Existent Notification

    [Test]
    public async Task Workflow_MarkAsRead_NonExistentNotification_Fails()
    {
        // Arrange
        var notificationId = NotificationId.New();
        _mockNotificationRepository.Setup(r => r.GetByPublicIdAsync(notificationId))
            .ReturnsAsync((Notification?)null);

        // Act
        var result = await _useCase.MarkAsReadAsync(notificationId, UserId.New());

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Notification not found");
    }

    #endregion
}
