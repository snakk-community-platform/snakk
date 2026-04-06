using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Infrastructure.Mappers;
using Snakk.Shared.Enums;

namespace Snakk.Infrastructure.Tests.Mappers;

public class NotificationMapperTests
{
    #region FromPersistence Tests

    [Test]
    public async Task FromPersistence_MapsAllProperties_WithAllSources()
    {
        var recipientPublicId = Guid.NewGuid().ToString();
        var actorPublicId = Guid.NewGuid().ToString();
        var postPublicId = Guid.NewGuid().ToString();
        var discussionPublicId = Guid.NewGuid().ToString();
        var spacePublicId = Guid.NewGuid().ToString();
        var readAt = DateTime.UtcNow.AddMinutes(-5);

        var entity = new UserNotificationDatabaseEntity
        {
            PublicId = "notif_abc",
            TypeId = (int)NotificationTypeEnum.Reply,
            Title = "New Reply",
            Body = "Someone replied to your post",
            IsRead = true,
            CreatedAt = new DateTime(2024, 6, 15, 10, 0, 0, DateTimeKind.Utc),
            ReadAt = readAt,
            RecipientUserId = 1,
            RecipientUser = new UserDatabaseEntity
            {
                PublicId = recipientPublicId,
                DisplayName = "Recipient",
                Email = "r@e.com",
                CreatedAt = DateTime.UtcNow
            },
            ActorUserId = 2,
            ActorUser = new UserDatabaseEntity
            {
                PublicId = actorPublicId,
                DisplayName = "Actor",
                Email = "a@e.com",
                CreatedAt = DateTime.UtcNow
            },
            SourcePostId = 1,
            SourcePost = new PostDatabaseEntity
            {
                PublicId = postPublicId,
                Content = "Post",
                CreatedAt = DateTime.UtcNow,
                LastModifiedAt = DateTime.UtcNow
            },
            SourceDiscussionId = 1,
            SourceDiscussion = new DiscussionDatabaseEntity
            {
                PublicId = discussionPublicId,
                Title = "Discussion",
                Slug = "discussion",
                CreatedAt = DateTime.UtcNow
            },
            SourceSpaceId = 1,
            SourceSpace = new SpaceDatabaseEntity
            {
                PublicId = spacePublicId,
                Name = "Space",
                Slug = "space",
                CreatedAt = DateTime.UtcNow,
                HubId = 1,
                Hub = new HubDatabaseEntity { PublicId = "h1", Name = "Hub", Slug = "hub", CreatedAt = DateTime.UtcNow, CommunityId = 1 }
            }
        };

        var notification = entity.FromPersistence();

        await Assert.That(notification).IsNotNull();
        await Assert.That(notification.PublicId.Value).IsEqualTo("notif_abc");
        await Assert.That(notification.RecipientUserId.Value).IsEqualTo(recipientPublicId);
        await Assert.That(notification.Title).IsEqualTo("New Reply");
        await Assert.That(notification.Body).IsEqualTo("Someone replied to your post");
        await Assert.That(notification.IsRead).IsTrue();
        await Assert.That(notification.ReadAt).IsEqualTo(readAt);
        await Assert.That(notification.ActorUserId).IsNotNull();
        await Assert.That(notification.ActorUserId!.Value).IsEqualTo(actorPublicId);
        await Assert.That(notification.SourcePostId).IsNotNull();
        await Assert.That(notification.SourcePostId!.Value).IsEqualTo(postPublicId);
        await Assert.That(notification.SourceDiscussionId).IsNotNull();
        await Assert.That(notification.SourceDiscussionId!.Value).IsEqualTo(discussionPublicId);
        await Assert.That(notification.SourceSpaceId).IsNotNull();
        await Assert.That(notification.SourceSpaceId!.Value).IsEqualTo(spacePublicId);
    }

    [Test]
    public async Task FromPersistence_WithNullSources_MapsNulls()
    {
        var entity = new UserNotificationDatabaseEntity
        {
            PublicId = "notif_nosrc",
            TypeId = (int)NotificationTypeEnum.Mention,
            Title = "Mention",
            Body = null,
            IsRead = false,
            CreatedAt = DateTime.UtcNow,
            RecipientUser = new UserDatabaseEntity
            {
                PublicId = "r1",
                DisplayName = "User",
                Email = "u@e.com",
                CreatedAt = DateTime.UtcNow
            },
            SourcePost = null,
            SourceDiscussion = null,
            SourceSpace = null,
            ActorUser = null
        };

        var notification = entity.FromPersistence();

        await Assert.That((object?)notification.SourcePostId).IsNull();
        await Assert.That((object?)notification.SourceDiscussionId).IsNull();
        await Assert.That((object?)notification.SourceSpaceId).IsNull();
        await Assert.That((object?)notification.ActorUserId).IsNull();
        await Assert.That(notification.Body).IsNull();
    }

    [Test]
    public async Task FromPersistence_MapsNotificationType()
    {
        var entity = new UserNotificationDatabaseEntity
        {
            PublicId = "notif_type",
            TypeId = (int)NotificationTypeEnum.NewPostInFollowedDiscussion,
            Title = "New Post",
            IsRead = false,
            CreatedAt = DateTime.UtcNow,
            RecipientUser = new UserDatabaseEntity
            {
                PublicId = "r2",
                DisplayName = "User",
                Email = "u@e.com",
                CreatedAt = DateTime.UtcNow
            }
        };

        var notification = entity.FromPersistence();

        await Assert.That(notification.Type).IsEqualTo(NotificationType.NewPostInFollowedDiscussion);
    }

    #endregion

    #region ToPersistence Tests

    [Test]
    public async Task ToPersistence_MapsAllProperties()
    {
        var notification = Notification.Rehydrate(
            NotificationId.From("notif_to"),
            UserId.From("recipient"),
            NotificationType.Reply,
            "Title",
            "Body",
            PostId.From("p1"),
            DiscussionId.From("d1"),
            SpaceId.From("s1"),
            UserId.From("actor"),
            false,
            DateTime.UtcNow,
            null);

        var entity = notification.ToPersistence();

        await Assert.That(entity).IsNotNull();
        await Assert.That(entity.PublicId).IsEqualTo("notif_to");
        await Assert.That(entity.TypeId).IsEqualTo((int)NotificationTypeEnum.Reply);
        await Assert.That(entity.Title).IsEqualTo("Title");
        await Assert.That(entity.Body).IsEqualTo("Body");
        await Assert.That(entity.IsRead).IsFalse();
        await Assert.That(entity.ReadAt).IsNull();
    }

    [Test]
    public async Task ToPersistence_ReadNotification_MapsReadAt()
    {
        var readAt = DateTime.UtcNow.AddMinutes(-10);
        var notification = Notification.Rehydrate(
            NotificationId.From("notif_read"),
            UserId.From("recipient"),
            NotificationType.Mention,
            "Title",
            null,
            null, null, null, null,
            true,
            DateTime.UtcNow,
            readAt);

        var entity = notification.ToPersistence();

        await Assert.That(entity.IsRead).IsTrue();
        await Assert.That(entity.ReadAt).IsEqualTo(readAt);
    }

    #endregion
}
