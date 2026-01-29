namespace Snakk.Infrastructure.Mappers;

using Snakk.Infrastructure.Database.Entities;
using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;

public static class CommunityMapper
{
    public static Community FromPersistence(this CommunityDatabaseEntity entity)
    {
        return Community.Rehydrate(
            CommunityId.From(entity.PublicId),
            entity.Name,
            entity.Slug,
            entity.Description,
            Enum.Parse<CommunityVisibility>(entity.Visibility),
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
            Visibility = community.Visibility.ToString(),
            ExposeToPlatformFeed = community.ExposeToPlatformFeed,
            CreatedAt = community.CreatedAt,
            LastModifiedAt = community.LastModifiedAt
        };
    }
}
