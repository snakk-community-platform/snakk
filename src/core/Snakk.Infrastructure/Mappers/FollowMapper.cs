namespace Snakk.Infrastructure.Mappers;

using Snakk.Domain.Entities;
using Snakk.Domain.Extensions;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Shared.Enums;

public static class FollowMapper
{
    public static Follow FromPersistence(this FollowDatabaseEntity entity)
    {
        return Follow.Rehydrate(
            FollowId.From(entity.PublicId),
            UserId.From(entity.User.PublicId),
            ((FollowTargetTypeEnum)entity.TargetTypeId).ToDomain(),
            entity.Discussion != null ? DiscussionId.From(entity.Discussion.PublicId) : null,
            entity.Space != null ? SpaceId.From(entity.Space.PublicId) : null,
            entity.FollowedUser != null ? UserId.From(entity.FollowedUser.PublicId) : null,
            ((FollowLevelEnum)entity.LevelId).ToDomain(),
            entity.CreatedAt);
    }

    public static FollowDatabaseEntity ToPersistence(this Follow follow)
    {
        return new FollowDatabaseEntity
        {
            PublicId = follow.PublicId,
            TargetTypeId = (int)follow.TargetType.ToShared(),
            LevelId = (int)follow.Level.ToShared(),
            CreatedAt = follow.CreatedAt
            // Foreign keys are set by the adapter
        };
    }
}
