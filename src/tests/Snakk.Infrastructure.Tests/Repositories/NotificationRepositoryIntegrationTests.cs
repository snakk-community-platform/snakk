using Snakk.Infrastructure.Database.Entities;
using Snakk.Infrastructure.Database.Repositories;
using Snakk.Infrastructure.Tests.Helpers;
using Snakk.Shared.Enums;

namespace Snakk.Infrastructure.Tests.Repositories;

public class NotificationRepositoryIntegrationTests : IDisposable
{
    private readonly SqliteTestDatabase _db;
    private readonly TestDataBuilder _builder;
    private readonly NotificationDatabaseRepository _repository;

    public NotificationRepositoryIntegrationTests()
    {
        _db = new SqliteTestDatabase();
        _builder = new TestDataBuilder(_db.Context);
        _repository = new NotificationDatabaseRepository(_db.Context);
    }

    public void Dispose() => _db.Dispose();

    #region Helper Methods

    private async Task<NotificationDatabaseEntity> CreateNotificationAsync(
        int recipientUserId,
        int? actorUserId = null,
        NotificationTypeEnum type = NotificationTypeEnum.Reply,
        string? title = null,
        string? body = null,
        bool isRead = false,
        DateTime? createdAt = null,
        int? sourcePostId = null,
        int? sourceDiscussionId = null,
        int? sourceSpaceId = null,
        string? publicId = null)
    {
        var notification = new NotificationDatabaseEntity
        {
            PublicId = publicId ?? $"notif-{Guid.NewGuid():N}",
            RecipientUserId = recipientUserId,
            ActorUserId = actorUserId,
            TypeId = (int)type,
            Title = title ?? "Test notification",
            Body = body,
            IsRead = isRead,
            CreatedAt = createdAt ?? DateTime.UtcNow,
            ReadAt = isRead ? DateTime.UtcNow : null,
            SourcePostId = sourcePostId,
            SourceDiscussionId = sourceDiscussionId,
            SourceSpaceId = sourceSpaceId
        };
        _db.Context.Notifications.Add(notification);
        await _db.Context.SaveChangesAsync();
        return notification;
    }

    #endregion

    #region GetByPublicIdAsync Tests

    [Test]
    public async Task GetByPublicIdAsync_ExistingNotification_ReturnsWithNavigations()
    {
        var (user, community, hub, space, discussion, post) = await _builder.CreateFullHierarchyAsync();
        var actor = await _builder.CreateUserAsync("ActorUser");

        var notification = await CreateNotificationAsync(
            user.Id,
            actorUserId: actor.Id,
            title: "Someone replied to your post",
            sourcePostId: post.Id,
            sourceDiscussionId: discussion.Id,
            sourceSpaceId: space.Id,
            publicId: "notif-nav-test");

        var result = await _repository.GetByPublicIdAsync("notif-nav-test");

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.PublicId).IsEqualTo("notif-nav-test");
        await Assert.That(result.RecipientUser).IsNotNull();
        await Assert.That(result.ActorUser).IsNotNull();
        await Assert.That(result.ActorUser!.DisplayName).IsEqualTo("ActorUser");
        await Assert.That(result.SourcePost).IsNotNull();
        await Assert.That(result.SourceDiscussion).IsNotNull();
        await Assert.That(result.SourceSpace).IsNotNull();
    }

    [Test]
    public async Task GetByPublicIdAsync_NonExistentPublicId_ReturnsNull()
    {
        var result = await _repository.GetByPublicIdAsync("nonexistent-notif-id");

        await Assert.That(result).IsNull();
    }

    #endregion

    #region GetByUserIdAsync Tests

    [Test]
    public async Task GetByUserIdAsync_ReturnsPaginatedResults()
    {
        var user = await _builder.CreateUserAsync();

        // Create 5 notifications
        for (var i = 0; i < 5; i++)
        {
            await CreateNotificationAsync(
                user.Id,
                title: $"Notification {i}",
                createdAt: DateTime.UtcNow.AddMinutes(-i));
        }

        var result = await _repository.GetByUserIdAsync(user.Id, offset: 0, pageSize: 3);

        await Assert.That(result.Items.Count()).IsEqualTo(3);
        await Assert.That(result.HasMoreItems).IsTrue();
        await Assert.That(result.Offset).IsEqualTo(0);
        await Assert.That(result.PageSize).IsEqualTo(3);
    }

    [Test]
    public async Task GetByUserIdAsync_ReturnsOrderedByCreatedAtDescending()
    {
        var user = await _builder.CreateUserAsync();

        var older = await CreateNotificationAsync(
            user.Id,
            title: "Older notification",
            createdAt: DateTime.UtcNow.AddHours(-2));

        var newer = await CreateNotificationAsync(
            user.Id,
            title: "Newer notification",
            createdAt: DateTime.UtcNow);

        var result = await _repository.GetByUserIdAsync(user.Id, offset: 0, pageSize: 10);
        var items = result.Items.ToList();

        await Assert.That(items.Count).IsEqualTo(2);
        await Assert.That(items[0].Id).IsEqualTo(newer.Id);
        await Assert.That(items[1].Id).IsEqualTo(older.Id);
    }

    [Test]
    public async Task GetByUserIdAsync_DoesNotReturnOtherUsersNotifications()
    {
        var user1 = await _builder.CreateUserAsync();
        var user2 = await _builder.CreateUserAsync();

        await CreateNotificationAsync(user1.Id, title: "User1 notification");
        await CreateNotificationAsync(user2.Id, title: "User2 notification");

        var result = await _repository.GetByUserIdAsync(user1.Id, offset: 0, pageSize: 10);

        await Assert.That(result.Items.Count()).IsEqualTo(1);
        await Assert.That(result.Items.First().RecipientUserId).IsEqualTo(user1.Id);
    }

    [Test]
    public async Task GetByUserIdAsync_HasMoreItems_FalseWhenNoMoreItems()
    {
        var user = await _builder.CreateUserAsync();

        await CreateNotificationAsync(user.Id, title: "Only notification");

        var result = await _repository.GetByUserIdAsync(user.Id, offset: 0, pageSize: 10);

        await Assert.That(result.Items.Count()).IsEqualTo(1);
        await Assert.That(result.HasMoreItems).IsFalse();
    }

    #endregion

    #region GetUnreadCountAsync Tests

    [Test]
    public async Task GetUnreadCountAsync_CountsOnlyUnreadNotifications()
    {
        var user = await _builder.CreateUserAsync();

        // 2 unread
        await CreateNotificationAsync(user.Id, isRead: false);
        await CreateNotificationAsync(user.Id, isRead: false);

        // 1 read
        await CreateNotificationAsync(user.Id, isRead: true);

        var count = await _repository.GetUnreadCountAsync(user.Id);

        await Assert.That(count).IsEqualTo(2);
    }

    [Test]
    public async Task GetUnreadCountAsync_DoesNotCountOtherUsersNotifications()
    {
        var user1 = await _builder.CreateUserAsync();
        var user2 = await _builder.CreateUserAsync();

        await CreateNotificationAsync(user1.Id, isRead: false);
        await CreateNotificationAsync(user2.Id, isRead: false);
        await CreateNotificationAsync(user2.Id, isRead: false);

        var count = await _repository.GetUnreadCountAsync(user1.Id);

        await Assert.That(count).IsEqualTo(1);
    }

    #endregion

    #region MarkAllAsReadAsync Tests

    [Test]
    public async Task MarkAllAsReadAsync_MarksAllUnreadAsRead()
    {
        var user = await _builder.CreateUserAsync();

        await CreateNotificationAsync(user.Id, isRead: false);
        await CreateNotificationAsync(user.Id, isRead: false);
        await CreateNotificationAsync(user.Id, isRead: true);

        await _repository.MarkAllAsReadAsync(user.Id);

        // Use a separate context to verify changes were persisted
        using var verifyContext = _db.CreateSeparateContext();
        var allNotifications = verifyContext.Notifications
            .Where(n => n.RecipientUserId == user.Id)
            .ToList();

        await Assert.That(allNotifications.Count).IsEqualTo(3);
        await Assert.That(allNotifications.All(n => n.IsRead)).IsTrue();
        await Assert.That(allNotifications.All(n => n.ReadAt != null)).IsTrue();
    }

    [Test]
    public async Task MarkAllAsReadAsync_DoesNotAffectOtherUsers()
    {
        var user1 = await _builder.CreateUserAsync();
        var user2 = await _builder.CreateUserAsync();

        await CreateNotificationAsync(user1.Id, isRead: false);
        await CreateNotificationAsync(user2.Id, isRead: false);

        await _repository.MarkAllAsReadAsync(user1.Id);

        // Use a separate context to verify changes
        using var verifyContext = _db.CreateSeparateContext();
        var user2Count = verifyContext.Notifications
            .Count(n => n.RecipientUserId == user2.Id && !n.IsRead);

        await Assert.That(user2Count).IsEqualTo(1);
    }

    #endregion
}
