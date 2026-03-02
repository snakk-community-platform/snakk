namespace Snakk.Infrastructure.Mappers;

using Snakk.Domain.Entities;
using Snakk.Domain.Extensions;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Shared.Enums;

public static class FollowMapper
{
    public static Follow FromPersistence(this FollowDatabaseEntity entity) =>
        Follow.Rehydrate(
            FollowId.From(entity.PublicId),
            UserId.From(entity.User.PublicId),
            ((FollowTargetTypeEnum)entity.TargetTypeId).ToDomain(),
            entity.Discussion is not null ? DiscussionId.From(entity.Discussion.PublicId) : null,
            entity.Space is not null ? SpaceId.From(entity.Space.PublicId) : null,
            entity.FollowedUser is not null ? UserId.From(entity.FollowedUser.PublicId) : null,
            ((FollowLevelEnum)entity.LevelId).ToDomain(),
            entity.CreatedAt);

    public static FollowDatabaseEntity ToPersistence(this Follow follow) =>
        new()
        {
            PublicId = follow.PublicId,
            TargetTypeId = (int)follow.TargetType.ToShared(),
            LevelId = (int)follow.Level.ToShared(),
            CreatedAt = follow.CreatedAt
            // Foreign keys are set by the adapter
        };
}
