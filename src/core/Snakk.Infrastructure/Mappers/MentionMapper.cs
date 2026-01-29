namespace Snakk.Infrastructure.Mappers;

using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Database.Entities;

public static class MentionMapper
{
    public static Mention FromPersistence(this MentionDatabaseEntity entity)
    {
        return Mention.Rehydrate(
            MentionId.From(entity.PublicId),
            PostId.From(entity.Post.PublicId),
            UserId.From(entity.MentionedUser.PublicId),
            entity.CreatedAt);
    }

    public static MentionDatabaseEntity ToPersistence(this Mention mention)
    {
        return new MentionDatabaseEntity
        {
            PublicId = mention.PublicId,
            CreatedAt = mention.CreatedAt
            // Foreign keys are set by the adapter
        };
    }
}
