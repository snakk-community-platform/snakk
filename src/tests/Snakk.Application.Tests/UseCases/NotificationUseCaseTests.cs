using NSubstitute;
using Snakk.Application.Services;
using Snakk.Application.UseCases;
using Snakk.Domain.Entities;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Models;

namespace Snakk.Application.Tests.UseCases;

public class NotificationUseCaseTests
{
    private readonly INotificationRepository _notificationRepository = Substitute.For<INotificationRepository>();
    private readonly IRealtimeNotifier _realtimeNotifier = Substitute.For<IRealtimeNotifier>();
    private readonly ICounterService _counterService = Substitute.For<ICounterService>();
    private NotificationUseCase _useCase = null!;

    [Before(Test)]
    public void Setup()
    {
        _useCase = new NotificationUseCase(
            _notificationRepository,
            _realtimeNotifier,
            _counterService);
    }

    #region GetNotificationsAsync Tests

    [Test]
    public async Task GetNotificationsAsync_ReturnsPagedNotificationDtos()
    {
        var userId = UserId.New();
        var postId = PostId.New();
        var discussionId = DiscussionId.New();
        var actorUserId = UserId.New();
        var notification = Notification.CreateForMention(userId, actorUserId, postId, discussionId, "Mentioner", "Test Discussion");
        var pagedResult = new PagedResult<Notification> { Items = [notification], Offset = 0, PageSize = 20, HasMoreItems = false };

        _notificationRepository.GetByUserIdAsync(userId, 0, 20).Returns(pagedResult);

        var result = await _useCase.GetNotificationsAsync(userId, 0, 20);

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
        var userId = UserId.New();
        var pagedResult = new PagedResult<Notification> { Items = [], Offset = 0, PageSize = 20, HasMoreItems = false };
        _notificationRepository.GetByUserIdAsync(userId, 0, 20).Returns(pagedResult);

        var result = await _useCase.GetNotificationsAsync(userId, 0, 20);

        await Assert.That(result.Items).Count().IsEqualTo(0);
    }

    [Test]
    public async Task GetNotificationsAsync_WithMultipleTypes_MapsAllCorrectly()
    {
        var userId = UserId.New();
        var actorUserId = UserId.New();
        var postId = PostId.New();
        var discussionId = DiscussionId.New();
        var mentionNotification = Notification.CreateForMention(userId, actorUserId, postId, discussionId, "User1", "Discussion 1");
        var replyNotification = Notification.CreateForReply(userId, actorUserId, postId, discussionId, "User1", "Discussion 1");
        var pagedResult = new PagedResult<Notification> { Items = [mentionNotification, replyNotification], Offset = 0, PageSize = 20, HasMoreItems = false };
        _notificationRepository.GetByUserIdAsync(userId, 0, 20).Returns(pagedResult);

        var result = await _useCase.GetNotificationsAsync(userId, 0, 20);

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
        var userId = UserId.New();
        _notificationRepository.GetUnreadCountAsync(userId).Returns(7);

        var result = await _useCase.GetUnreadCountAsync(userId);

        await Assert.That(result).IsEqualTo(7);
    }

    [Test]
    public async Task GetUnreadCountAsync_WithNoUnread_ReturnsZero()
    {
        var userId = UserId.New();
        _notificationRepository.GetUnreadCountAsync(userId).Returns(0);

        var result = await _useCase.GetUnreadCountAsync(userId);

        await Assert.That(result).IsEqualTo(0);
    }

    #endregion

    #region MarkAsReadAsync Tests

    [Test]
    public async Task MarkAsReadAsync_WithValidNotification_MarksAsRead()
    {
        var userId = UserId.New();
        var notification = Notification.CreateForMention(userId, UserId.New(), PostId.New(), DiscussionId.New(), "User1", "Discussion");
        var notificationId = notification.PublicId;
        _notificationRepository.GetByPublicIdAsync(notificationId).Returns(notification);
        _notificationRepository.GetUnreadCountAsync(userId).Returns(3);

        var result = await _useCase.MarkAsReadAsync(notificationId, userId);

        await Assert.That(result.IsSuccess).IsTrue();
        await Assert.That(notification.IsRead).IsTrue();
        await _notificationRepository.Received(1).UpdateAsync(notification);
        await _realtimeNotifier.Received(1).NotifyUnreadCountUpdatedAsync(userId, 3);
    }

    [Test]
    public async Task MarkAsReadAsync_WithNonExistentNotification_ReturnsFailure()
    {
        var notificationId = NotificationId.New();
        var userId = UserId.New();
        _notificationRepository.GetByPublicIdAsync(notificationId).Returns((Notification?)null);

        var result = await _useCase.MarkAsReadAsync(notificationId, userId);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Notification not found");
    }

    [Test]
    public async Task MarkAsReadAsync_ByDifferentUser_ReturnsFailure()
    {
        var ownerId = UserId.New();
        var differentUserId = UserId.New();
        var notification = Notification.CreateForMention(ownerId, UserId.New(), PostId.New(), DiscussionId.New(), "User1", "Discussion");
        _notificationRepository.GetByPublicIdAsync(notification.PublicId).Returns(notification);

        var result = await _useCase.MarkAsReadAsync(notification.PublicId, differentUserId);

        await Assert.That(result.IsSuccess).IsFalse();
        await Assert.That(result.Error).Contains("Cannot mark other user's notification");
        await _notificationRepository.DidNotReceive().UpdateAsync(Arg.Any<Notification>());
    }

    #endregion

    #region MarkAllAsReadAsync Tests

    [Test]
    public async Task MarkAllAsReadAsync_MarksAllAndNotifiesClient()
    {
        var userId = UserId.New();

        await _useCase.MarkAllAsReadAsync(userId);

        await _notificationRepository.Received(1).MarkAllAsReadAsync(userId);
        await _realtimeNotifier.Received(1).NotifyUnreadCountUpdatedAsync(userId, 0);
    }

    #endregion

    #region CreateNotificationAsync Tests

    [Test]
    public async Task CreateNotificationAsync_PersistsAndNotifiesRealtime()
    {
        var recipientUserId = UserId.New();
        var actorUserId = UserId.New();
        var postId = PostId.New();
        var discussionId = DiscussionId.New();
        var notification = Notification.CreateForMention(recipientUserId, actorUserId, postId, discussionId, "Mentioner", "Test Discussion");

        await _useCase.CreateNotificationAsync(notification);

        await _notificationRepository.Received(1).AddAsync(notification);
        await _realtimeNotifier.Received(1).NotifyUserAsync(recipientUserId, Arg.Any<object>());
    }

    [Test]
    public async Task CreateNotificationAsync_ForReply_PersistsAndNotifies()
    {
        var recipientUserId = UserId.New();
        var replierUserId = UserId.New();
        var postId = PostId.New();
        var discussionId = DiscussionId.New();
        var notification = Notification.CreateForReply(recipientUserId, replierUserId, postId, discussionId, "Replier", "Discussion Title");

        await _useCase.CreateNotificationAsync(notification);

        await _notificationRepository.Received(1).AddAsync(notification);
        await _realtimeNotifier.Received(1).NotifyUserAsync(recipientUserId, Arg.Any<object>());
    }

    #endregion
}
