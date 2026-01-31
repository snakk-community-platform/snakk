namespace Snakk.Infrastructure.Mappers;

using Snakk.Infrastructure.Database.Entities;
using Snakk.Domain.Entities;
using Snakk.Domain.Extensions;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Enums;

public static class CommunityMapper
{
    public static Community FromPersistence(this CommunityDatabaseEntity entity)
    {
        return Community.Rehydrate(
            CommunityId.From(entity.PublicId),
            entity.Name,
            entity.Slug,
            entity.Description,
            ((CommunityVisibilityEnum)entity.VisibilityId).ToDomain(),
            entity.ExposeToPlatformFeed,
            entity.CreatedAt,
            entity.LastModifiedAt,
            hubs: []);
    }

    public static CommunityDatabaseEntity ToPersistence(this Community community)
    {
        return new CommunityDatabaseEntity
        {
            PublicId = community.PublicId,
            Name = community.Name,
            Slug = community.Slug,
            Description = community.Description,
            VisibilityId = (int)community.Visibility.ToShared(),
            ExposeToPlatformFeed = community.ExposeToPlatformFeed,
            CreatedAt = community.CreatedAt,
            LastModifiedAt = community.LastModifiedAt
        };
    }
}
