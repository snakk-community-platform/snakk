namespace Snakk.Infrastructure.Mappers;

using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Database.Entities;

public static class FollowMapper
{
    public static Follow FromPersistence(this FollowDatabaseEntity entity)
    {
        return Follow.Rehydrate(
            FollowId.From(entity.PublicId),
            UserId.From(entity.User.PublicId),
            Enum.Parse<FollowTargetType>(entity.TargetType),
            entity.Discussion != null ? DiscussionId.From(entity.Discussion.PublicId) : null,
            entity.Space != null ? SpaceId.From(entity.Space.PublicId) : null,
            entity.FollowedUser != null ? UserId.From(entity.FollowedUser.PublicId) : null,
            Enum.TryParse<FollowLevel>(entity.Level, out var level) ? level : FollowLevel.DiscussionsOnly,
            entity.CreatedAt);
    }

    public static FollowDatabaseEntity ToPersistence(this Follow follow)
    {
        return new FollowDatabaseEntity
        {
            PublicId = follow.PublicId,
            TargetType = follow.TargetType.ToString(),
            Level = follow.Level.ToString(),
            CreatedAt = follow.CreatedAt
            // Foreign keys are set by the adapter
        };
    }
}
