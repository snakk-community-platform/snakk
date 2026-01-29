namespace Snakk.Infrastructure.Mappers;

using Snakk.Infrastructure.Database.Entities;
using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;

public static class HubMapper
{
    public static Hub FromPersistence(this HubDatabaseEntity entity)
    {
        return Hub.Rehydrate(
            HubId.From(entity.PublicId),
            CommunityId.From(entity.Community.PublicId),
            entity.Name,
            entity.Slug,
            entity.Description,
            entity.AllowAnonymousReading,
            entity.RequireEmailConfirmation,
            entity.CreatedAt,
            entity.LastModifiedAt,
            spaces: []);
    }

    public static Hub FromPersistenceWithCommunityId(this HubDatabaseEntity entity, string communityPublicId)
    {
        return Hub.Rehydrate(
            HubId.From(entity.PublicId),
            CommunityId.From(communityPublicId),
            entity.Name,
            entity.Slug,
            entity.Description,
            entity.AllowAnonymousReading,
            entity.RequireEmailConfirmation,
            entity.CreatedAt,
            entity.LastModifiedAt,
            spaces: []);
    }

    public static HubDatabaseEntity ToPersistence(this Hub hub, int communityDbId)
    {
        return new HubDatabaseEntity
        {
            PublicId = hub.PublicId,
            CommunityId = communityDbId,
            Name = hub.Name,
            Slug = hub.Slug,
            Description = hub.Description,
            AllowAnonymousReading = hub.AllowAnonymousReading,
            RequireEmailConfirmation = hub.RequireEmailConfirmation,
            CreatedAt = hub.CreatedAt,
            LastModifiedAt = hub.LastModifiedAt
        };
    }
}
