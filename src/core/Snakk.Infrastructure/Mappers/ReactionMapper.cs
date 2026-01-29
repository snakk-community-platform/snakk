namespace Snakk.Infrastructure.Mappers;

using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Database.Entities;

public static class ReactionMapper
{
    public static Reaction FromPersistence(this ReactionDatabaseEntity entity)
    {
        return Reaction.Rehydrate(
            ReactionId.From(entity.PublicId),
            PostId.From(entity.Post.PublicId),
            UserId.From(entity.User.PublicId),
            Enum.Parse<ReactionType>(entity.Type),
            entity.CreatedAt);
    }

    public static ReactionDatabaseEntity ToPersistence(this Reaction reaction)
    {
        return new ReactionDatabaseEntity
        {
            PublicId = reaction.PublicId,
            Type = reaction.Type.ToString(),
            CreatedAt = reaction.CreatedAt
            // PostId and UserId are set by the adapter
        };
    }
}
