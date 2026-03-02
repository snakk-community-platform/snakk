using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Database.Entities;

namespace Snakk.Infrastructure.Mappers;

public static class RefreshTokenMapper
{
    public static RefreshToken FromPersistence(this RefreshTokenDatabaseEntity entity)
    {
        if (entity.User is null)
            throw new InvalidOperationException("User navigation property must be loaded");

        return RefreshToken.Rehydrate(
            entity.TokenValue,
            UserId.From(entity.User.PublicId),
            entity.ExpiresAt,
            entity.CreatedAt,
            entity.RevokedAt?.ToString("O")
        );
    }

    // Note: ToPersistence is not used because we need to look up User.Id from the database
    // Use repository methods directly instead
}
