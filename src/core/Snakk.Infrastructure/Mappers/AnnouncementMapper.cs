namespace Snakk.Infrastructure.Mappers;

using Snakk.Infrastructure.Database.Entities;
using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Enums;

public static class AnnouncementMapper
{
    public static Announcement FromPersistence(this AnnouncementDatabaseEntity entity) =>
        Announcement.Rehydrate(
            AnnouncementId.From(entity.PublicId),
            entity.Title,
            entity.Content,
            entity.RenderedContent,
            (AnnouncementTypeEnum)entity.TypeId,
            (AnnouncementScopeEnum)entity.ScopeId,
            entity.ScopeEntityId,
            entity.VisibleFrom,
            entity.VisibleUntil,
            entity.IsDismissible,
            entity.SortOrder,
            UserId.From(entity.CreatedByUser.PublicId),
            entity.CreatedAt,
            entity.LastModifiedAt);

    public static AnnouncementDatabaseEntity ToPersistence(this Announcement announcement) =>
        new()
        {
            PublicId = announcement.PublicId,
            Title = announcement.Title,
            Content = announcement.Content,
            RenderedContent = announcement.RenderedContent,
            TypeId = (int)announcement.Type,
            ScopeId = (int)announcement.Scope,
            ScopeEntityId = announcement.ScopeEntityId,
            VisibleFrom = announcement.VisibleFrom,
            VisibleUntil = announcement.VisibleUntil,
            IsDismissible = announcement.IsDismissible,
            SortOrder = announcement.SortOrder,
            CreatedAt = announcement.CreatedAt,
            LastModifiedAt = announcement.LastModifiedAt
            // CreatedByUserId will be set by repository adapter
        };
}
