namespace Snakk.Infrastructure.Mappers;

using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Database.Entities;

public static class PostRevisionMapper
{
    public static PostRevision FromPersistence(this PostRevisionDatabaseEntity entity)
    {
        return PostRevision.Rehydrate(
            PostId.From(entity.PostPublicId),
            entity.Content,
            UserId.From(entity.EditedByUserPublicId),
            entity.RevisionNumber,
            entity.CreatedAt);
    }

    public static PostRevisionDatabaseEntity ToPersistence(this PostRevision revision)
    {
        return new PostRevisionDatabaseEntity
        {
            PostPublicId = revision.PostId,
            Content = revision.Content,
            EditedByUserPublicId = revision.EditedByUserId,
            RevisionNumber = revision.RevisionNumber,
            CreatedAt = revision.CreatedAt
        };
    }
}
