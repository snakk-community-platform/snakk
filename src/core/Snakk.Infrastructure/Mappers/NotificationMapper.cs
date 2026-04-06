namespace Snakk.Infrastructure.Mappers;

using Snakk.Domain.Entities;
using Snakk.Domain.Extensions;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Shared.Enums;

public static class NotificationMapper
{
    public static Notification FromPersistence(this UserNotificationDatabaseEntity entity) =>
        Notification.Rehydrate(
            NotificationId.From(entity.PublicId),
            UserId.From(entity.RecipientUser.PublicId),
            ((NotificationTypeEnum)entity.TypeId).ToDomain(),
            entity.Title,
            entity.Body,
            entity.SourcePost is not null ? PostId.From(entity.SourcePost.PublicId) : null,
            entity.SourceDiscussion is not null ? DiscussionId.From(entity.SourceDiscussion.PublicId) : null,
            entity.SourceSpace is not null ? SpaceId.From(entity.SourceSpace.PublicId) : null,
            entity.ActorUser is not null ? UserId.From(entity.ActorUser.PublicId) : null,
            entity.IsRead,
            entity.CreatedAt,
            entity.ReadAt);

    public static UserNotificationDatabaseEntity ToPersistence(this Notification notification) =>
        new()
        {
            PublicId = notification.PublicId,
            TypeId = (int)notification.Type.ToShared(),
            Title = notification.Title,
            Body = notification.Body,
            IsRead = notification.IsRead,
            CreatedAt = notification.CreatedAt,
            ReadAt = notification.ReadAt
            // Foreign keys are set by the adapter
        };
}
