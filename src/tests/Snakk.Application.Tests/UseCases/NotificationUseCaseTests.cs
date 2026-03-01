using Moq;
using Snakk.Application.Services;
using Snakk.Application.UseCases;
using Snakk.Domain.Entities;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Models;

namespace Snakk.Application.Tests.UseCases;

public class NotificationUseCaseTests
{
    private readonly Mock<INotificationRepository> _mockNotificationRepository = new();
    private readonly Mock<IRealtimeNotifier> _mockRealtimeNotifier = new();
    private readonly Mock<ICounterService> _mockCounterService = new();
    private NotificationUseCase _useCase = null!;

    [Before(Test)]
    public void Setup()
    {
        _useCase = new NotificationUseCase(
            _mockNotificationRepository.Object,
            _mockRealtimeNotifier.Object,
            _mockCounterService.Object);
    }

    #region GetNotificationsAsync Tests

    [Test]
    public async Task GetNotificationsAsync_ReturnsPagedNotificationDtos()
    {
        // Arrange
        var userId = UserId.New();
        var postId = PostId.New();
        var discussionId = DiscussionId.New();
        var actorUserId = UserId.New();

        var notification = Notification.CreateForMention(
            userId, actorUserId, postId, discussionId, "Mentioner", "Test Discussion");

        var pagedResult = new PagedResult<Notification>
        {
            Items = new List<Notification> { notification },
            Offset = 0,
            PageSize = 20,
            HasMoreItems = false
        };

        _mockNotificationRepository.Setup(r => r.GetByUserIdAsync(userId, 0, 20))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _useCase.GetNotificationsAsync(userId, 0, 20);

        // Assert
        await Assert.That(result).IsNotNull();
        await Assert.That(result.Items).Count().IsEqualTo(1);
        await Assert.That(result.Offset).IsEqualTo(0);
        await Assert.That(result.PageSize).IsEqualTo(20);

        var dto = result.Items.First();
        await Assert.That(dto.Type).IsEqualTo("Mention");
        await Assert.That(dto.IsRead).IsFalse();
        await Assert.That(dto.SourcePostId).IsEqualTo(postId.Value);
        await Assert.That(dto.SourceDiscussionId).IsEqualTo(discussionId.Value);
        await Assert.That(dto.ActorUserId).IsEqualTo(actorUserId.Value);
    }

    [Test]
    public async Task GetNotificationsAsync_WithEmptyResults_ReturnsEmptyList()
    {
        // Arrange
        var userId = UserId.New();

        var pagedResult = new PagedResult<Notification>
        {
            Items = [],
            Offset = 0,
            PageSize = 20,
            HasMoreItems = false
        };

        _mockNotificationRepository.Setup(r => r.GetByUserIdAsync(userId, 0, 20))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _useCase.GetNotificationsAsync(userId, 0, 20);

        // Assert
        await Assert.That(result.Items).Count().IsEqualTo(0);
    }

    [Test]
    public async Task GetNotificationsAsync_WithMultipleTypes_MapsAllCorrectly()
    {
        // Arrange
        var userId = UserId.New();
        var actorUserId = UserId.New();
        var postId = PostId.New();
        var discussionId = DiscussionId.New();

        var mentionNotification = Notification.CreateForMention(
            userId, actorUserId, postId, discussionId, "User1", "Discussion 1");
        var replyNotification = Notification.CreateForReply(
            userId, actorUserId, postId, discussionId, "User1", "Discussion 1");

        var pagedResult = new PagedResult<Notification>
        {
            Items = new List<Notification> { mentionNotification, replyNotification },
            Offset = 0,
            PageSize = 20,
            HasMoreItems = false
        };

        _mockNotificationRepository.Setup(r => r.GetByUserIdAsync(userId, 0, 20))
            .ReturnsAsync(pagedResult);

        // Act
        var result = await _useCase.GetNotificationsAsync(userId, 0, 20);

        // Assert
        await Assert.That(result.Items).Count().IsEqualTo(2);
        var items = result.Items.ToList();
        await Assert.That(items[0].Type).IsEqualTo("Mention");
        await Assert.That(items[1].Type).IsEqualTo("Reply");
    }

    #endregion

    #region GetUnreadCountAsync Tests

    [Test]
    public async Task GetUnreadCountAsync_ReturnsCount()
    {
        // Arrange
        var userId = UserId.New();

        _mockNotificationRepository.Setup(r => r.GetUnreadCountAsync(userId))
            .ReturnsAsync(7);

        // Act
        var result = await _useCase.GetUnreadCountAsync(userId);

        // Assert
        await Assert.That(result).IsEqualTo(7);
    }

    [Test]
    public async Task GetUnreadCountAsync_WithNoUnread_ReturnsZero()
    {
        // Arrange
        var userId = UserId.New();

        _mockNotificationRepository.Setup(r => r.GetUnreadCountAsync(userId))
            .ReturnsAsync(0);

        // Act
        var result = await _useCase.GetUnreadCountAsync(userId);

        // Assert
        await Assert.That(result).IsEqualTo(0);
    }

    #endregion

    #region MarkAsReadAsync Tests

    [Test]
    public async Task MarkAsReadAsync_WithValidNotification_MarksAsRead()
    {
        // Arrange
        var userId = UserId.New();
        var notification = Notification.CreateForMention(
            userId, UserId.New(), PostId.New(), DiscussionId.New(), "User1", "Discussion");
        var notificationId = notification.PublicId;

        _mockNotificationRepository.Setup(r => r.GetByPublicIdAsync(notificationId))
            .ReturnsAsync(notification);
        _mockNotificationRepository.Setup(r => r.GetUnreadCountAsync(userId))
            .ReturnsAsync(3);

        // Act
        var result = await _useCase.MarkAsReadAsync(notificationId, userId);

        // Assert
        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(notification.IsRead).IsTrue();

        _mockNotificationRepository.Verify(r => r.UpdateAsync(notification), Times.Once);
        _mockRealtimeNotifier.Verify(r => r.NotifyUnreadCountUpdatedAsync(userId, 3), Times.Once);
    }

    [Test]
    public async Task MarkAsReadAsync_WithNonExistentNotification_ReturnsFailure()
    {
        // Arrange
        var notificationId = NotificationId.New();
        var userId = UserId.New();

        _mockNotificationRepository.Setup(r => r.GetByPublicIdAsync(notificationId))
            .ReturnsAsync((Notification?)null);

        // Act
        var result = await _useCase.MarkAsReadAsync(notificationId, userId);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Notification not found");
    }

    [Test]
    public async Task MarkAsReadAsync_ByDifferentUser_ReturnsFailure()
    {
        // Arrange
        var ownerId = UserId.New();
        var differentUserId = UserId.New();
        var notification = Notification.CreateForMention(
            ownerId, UserId.New(), PostId.New(), DiscussionId.New(), "User1", "Discussion");

        _mockNotificationRepository.Setup(r => r.GetByPublicIdAsync(notification.PublicId))
            .ReturnsAsync(notification);

        // Act
        var result = await _useCase.MarkAsReadAsync(notification.PublicId, differentUserId);

        // Assert
        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Cannot mark other user's notification");

        _mockNotificationRepository.Verify(r => r.UpdateAsync(It.IsAny<Notification>()), Times.Never);
    }

    #endregion

    #region MarkAllAsReadAsync Tests

    [Test]
    public async Task MarkAllAsReadAsync_MarksAllAndNotifiesClient()
    {
        // Arrange
        var userId = UserId.New();

        // Act
        await _useCase.MarkAllAsReadAsync(userId);

        // Assert
        _mockNotificationRepository.Verify(r => r.MarkAllAsReadAsync(userId), Times.Once);
        _mockRealtimeNotifier.Verify(r => r.NotifyUnreadCountUpdatedAsync(userId, 0), Times.Once);
    }

    #endregion

    #region CreateNotificationAsync Tests

    [Test]
    public async Task CreateNotificationAsync_PersistsAndNotifiesRealtime()
    {
        // Arrange
        var recipientUserId = UserId.New();
        var actorUserId = UserId.New();
        var postId = PostId.New();
        var discussionId = DiscussionId.New();

        var notification = Notification.CreateForMention(
            recipientUserId, actorUserId, postId, discussionId, "Mentioner", "Test Discussion");

        // Act
        await _useCase.CreateNotificationAsync(notification);

        // Assert
        _mockNotificationRepository.Verify(r => r.AddAsync(notification), Times.Once);
        _mockRealtimeNotifier.Verify(r => r.NotifyUserAsync(recipientUserId, It.IsAny<object>()), Times.Once);
    }

    [Test]
    public async Task CreateNotificationAsync_ForReply_PersistsAndNotifies()
    {
        // Arrange
        var recipientUserId = UserId.New();
        var replierUserId = UserId.New();
        var postId = PostId.New();
        var discussionId = DiscussionId.New();

        var notification = Notification.CreateForReply(
            recipientUserId, replierUserId, postId, discussionId, "Replier", "Discussion Title");

        // Act
        await _useCase.CreateNotificationAsync(notification);

        // Assert
        _mockNotificationRepository.Verify(r => r.AddAsync(notification), Times.Once);
        _mockRealtimeNotifier.Verify(r => r.NotifyUserAsync(recipientUserId, It.IsAny<object>()), Times.Once);
    }

    #endregion
}
