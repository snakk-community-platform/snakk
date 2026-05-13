namespace Snakk.Infrastructure.Mappers;

using Snakk.Infrastructure.Database.Entities;
using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Enums;

public static class BannerMapper
{
    public static Banner FromPersistence(this BannerDatabaseEntity entity) =>
        Banner.Rehydrate(
            BannerId.From(entity.PublicId),
            entity.Title,
            entity.Content,
            entity.RenderedContent,
            (BannerTypeEnum)entity.TypeId,
            (BannerScopeEnum)entity.ScopeId,
            entity.ScopeEntityId,
            entity.VisibleFrom,
            entity.VisibleUntil,
            entity.IsDismissible,
            entity.SortOrder,
            UserId.From(entity.CreatedByUserPublicId ?? entity.CreatedByUser?.PublicId ?? ""),
            entity.CreatedAt,
            entity.LastModifiedAt);

    public static BannerDatabaseEntity ToPersistence(this Banner banner) =>
        new()
        {
            PublicId = banner.PublicId,
            Title = banner.Title,
            Content = banner.Content,
            RenderedContent = banner.RenderedContent,
            TypeId = (int)banner.Type,
            ScopeId = (int)banner.Scope,
            ScopeEntityId = banner.ScopeEntityId,
            VisibleFrom = banner.VisibleFrom,
            VisibleUntil = banner.VisibleUntil,
            IsDismissible = banner.IsDismissible,
            SortOrder = banner.SortOrder,
            CreatedAt = banner.CreatedAt,
            LastModifiedAt = banner.LastModifiedAt
            // CreatedByUserId will be set by repository adapter
        };
}
