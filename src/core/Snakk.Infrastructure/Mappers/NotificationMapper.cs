namespace Snakk.Infrastructure.Mappers;

using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Database.Entities;

public static class NotificationMapper
{
    public static Notification FromPersistence(this NotificationDatabaseEntity entity)
    {
        return Notification.Rehydrate(
            NotificationId.From(entity.PublicId),
            UserId.From(entity.RecipientUser.PublicId),
            Enum.Parse<NotificationType>(entity.Type),
            entity.Title,
            entity.Body,
            entity.SourcePost != null ? PostId.From(entity.SourcePost.PublicId) : null,
            entity.SourceDiscussion != null ? DiscussionId.From(entity.SourceDiscussion.PublicId) : null,
            entity.SourceSpace != null ? SpaceId.From(entity.SourceSpace.PublicId) : null,
            entity.ActorUser != null ? UserId.From(entity.ActorUser.PublicId) : null,
            entity.IsRead,
            entity.CreatedAt,
            entity.ReadAt);
    }

    public static NotificationDatabaseEntity ToPersistence(this Notification notification)
    {
        return new NotificationDatabaseEntity
        {
            PublicId = notification.PublicId,
            Type = notification.Type.ToString(),
            Title = notification.Title,
            Body = notification.Body,
            IsRead = notification.IsRead,
            CreatedAt = notification.CreatedAt,
            ReadAt = notification.ReadAt
            // Foreign keys are set by the adapter
        };
    }
}
