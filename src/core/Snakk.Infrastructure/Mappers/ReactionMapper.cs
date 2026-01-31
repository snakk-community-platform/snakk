namespace Snakk.Infrastructure.Mappers;

using Snakk.Domain.Entities;
using Snakk.Domain.Extensions;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Shared.Enums;

public static class ReactionMapper
{
    public static Reaction FromPersistence(this ReactionDatabaseEntity entity)
    {
        return Reaction.Rehydrate(
            ReactionId.From(entity.PublicId),
            PostId.From(entity.Post.PublicId),
            UserId.From(entity.User.PublicId),
            ((ReactionTypeEnum)entity.TypeId).ToDomain(),
            entity.CreatedAt);
    }

    public static ReactionDatabaseEntity ToPersistence(this Reaction reaction)
    {
        return new ReactionDatabaseEntity
        {
            PublicId = reaction.PublicId,
            TypeId = (int)reaction.Type.ToShared(),
            CreatedAt = reaction.CreatedAt
            // PostId and UserId are set by the adapter
        };
    }
}
