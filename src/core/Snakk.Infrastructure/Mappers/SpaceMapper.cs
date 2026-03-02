namespace Snakk.Infrastructure.Mappers;

using Snakk.Infrastructure.Database.Entities;
using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;

public static class SpaceMapper
{
    public static Space FromPersistence(this SpaceDatabaseEntity entity) =>
        Space.Rehydrate(
            SpaceId.From(entity.PublicId),
            HubId.From(entity.Hub.PublicId),
            entity.Name,
            entity.Slug,
            entity.Description,
            entity.AllowAnonymousReading,
            entity.RequireEmailConfirmation,
            entity.CreatedAt,
            entity.LastModifiedAt,
            discussions: []);

    public static SpaceDatabaseEntity ToPersistence(this Space space) =>
        new()
        {
            PublicId = space.PublicId,
            Name = space.Name,
            Slug = space.Slug,
            Description = space.Description,
            AllowAnonymousReading = space.AllowAnonymousReading,
            RequireEmailConfirmation = space.RequireEmailConfirmation,
            CreatedAt = space.CreatedAt,
            LastModifiedAt = space.LastModifiedAt
            // HubId will be set by repository adapter
        };
}
