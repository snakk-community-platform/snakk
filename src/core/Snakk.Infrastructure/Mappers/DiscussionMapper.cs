namespace Snakk.Infrastructure.Mappers;

using Snakk.Infrastructure.Database.Entities;
using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Enums;

public static class DiscussionMapper
{
    public static Discussion FromPersistence(this DiscussionDatabaseEntity entity) =>
        Discussion.Rehydrate(
            DiscussionId.From(entity.PublicId),
            SpaceId.From(entity.Space.PublicId),
            UserId.From(entity.CreatedByUser.PublicId),
            entity.Title,
            entity.Slug,
            (DiscussionTypeEnum)entity.Type,
            entity.CreatedAt,
            entity.LastModifiedAt,
            entity.LastActivityAt,
            entity.IsPinned,
            entity.IsLocked,
            posts: []);

    public static DiscussionDatabaseEntity ToPersistence(this Discussion discussion) =>
        // Note: Navigation properties (Space, CreatedByUser) must be set separately
        // in the repository adapter after fetching them by PublicId
        new()
        {
            PublicId = discussion.PublicId,
            Title = discussion.Title,
            Slug = discussion.Slug,
            Type = (int)discussion.Type,
            CreatedAt = discussion.CreatedAt,
            LastModifiedAt = discussion.LastModifiedAt,
            LastActivityAt = discussion.LastActivityAt,
            IsPinned = discussion.IsPinned,
            IsLocked = discussion.IsLocked
            // SpaceId and CreatedByUserId will be set by repository adapter
        };
}
