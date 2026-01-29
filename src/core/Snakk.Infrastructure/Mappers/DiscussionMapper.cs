namespace Snakk.Infrastructure.Mappers;

using Snakk.Infrastructure.Database.Entities;
using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;

public static class DiscussionMapper
{
    public static Discussion FromPersistence(this DiscussionDatabaseEntity entity)
    {
        return Discussion.Rehydrate(
            DiscussionId.From(entity.PublicId),
            SpaceId.From(entity.Space.PublicId),
            UserId.From(entity.CreatedByUser.PublicId),
            entity.Title,
            entity.Slug,
            entity.CreatedAt,
            entity.LastModifiedAt,
            entity.LastActivityAt,
            entity.IsPinned,
            entity.IsLocked,
            posts: []);
    }

    public static DiscussionDatabaseEntity ToPersistence(this Discussion discussion)
    {
        // Note: Navigation properties (Space, CreatedByUser) must be set separately
        // in the repository adapter after fetching them by PublicId
        return new DiscussionDatabaseEntity
        {
            PublicId = discussion.PublicId,
            Title = discussion.Title,
            Slug = discussion.Slug,
            CreatedAt = discussion.CreatedAt,
            LastModifiedAt = discussion.LastModifiedAt,
            LastActivityAt = discussion.LastActivityAt,
            IsPinned = discussion.IsPinned,
            IsLocked = discussion.IsLocked
            // SpaceId and CreatedByUserId will be set by repository adapter
        };
    }
}
