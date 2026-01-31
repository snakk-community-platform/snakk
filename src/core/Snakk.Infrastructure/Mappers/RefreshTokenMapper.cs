using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Database.Entities;

namespace Snakk.Infrastructure.Mappers;

public static class RefreshTokenMapper
{
    public static RefreshToken FromPersistence(this RefreshTokenDatabaseEntity entity)
    {
        return RefreshToken.Rehydrate(
            entity.TokenValue,
            UserId.From(entity.UserId),
            entity.ExpiresAt,
            entity.CreatedAt,
            entity.RevokedAt
        );
    }

    public static RefreshTokenDatabaseEntity ToPersistence(this RefreshToken token)
    {
        return new RefreshTokenDatabaseEntity
        {
            TokenValue = token.Value,
            UserId = token.UserId.Value,
            ExpiresAt = token.ExpiresAt,
            CreatedAt = token.CreatedAt,
            RevokedAt = token.RevokedAt
        };
    }
}
