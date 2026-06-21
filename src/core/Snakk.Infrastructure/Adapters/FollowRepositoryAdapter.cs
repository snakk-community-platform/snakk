namespace Snakk.Infrastructure.Adapters;

using Microsoft.EntityFrameworkCore;
using Snakk.Domain.Entities;
using Snakk.Domain.Extensions;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Repositories;
using Snakk.Infrastructure.Mappers;
using Snakk.Shared.Enums;

public class FollowRepositoryAdapter(
    IFollowDatabaseRepository databaseRepository,
    SnakkDbContext context) : IFollowRepository
{
    public async Task<Follow?> GetByUserAndDiscussionAsync(UserId userId, DiscussionId discussionId, CancellationToken ct = default)
    {
        var projection = await context.UserFollows
            .Where(f =>
                f.UserPublicId == userId.Value
                && f.Discussion != null
                && f.DiscussionPublicId == discussionId.Value)
            .Select(f => new FollowProjection(
                f.PublicId, f.UserPublicId!, f.TargetTypeId,
                f.DiscussionPublicId,
                f.SpacePublicId,
                f.FollowedUserPublicId,
                f.LevelId, f.CreatedAt))
            .FirstOrDefaultAsync(ct);
        return projection?.ToDomain();
    }

    public async Task<Follow?> GetByUserAndSpaceAsync(UserId userId, SpaceId spaceId, CancellationToken ct = default)
    {
        var projection = await context.UserFollows
            .Where(f =>
                f.UserPublicId == userId.Value
                && f.Space != null
                && f.SpacePublicId == spaceId.Value)
            .Select(f => new FollowProjection(
                f.PublicId, f.UserPublicId!, f.TargetTypeId,
                f.DiscussionPublicId,
                f.SpacePublicId,
                f.FollowedUserPublicId,
                f.LevelId, f.CreatedAt))
            .FirstOrDefaultAsync(ct);
        return projection?.ToDomain();
    }

    public async Task<Follow?> GetByUserAndFollowedUserAsync(UserId userId, UserId followedUserId, CancellationToken ct = default)
    {
        var projection = await context.UserFollows
            .Where(f =>
                f.UserPublicId == userId.Value
                && f.FollowedUser != null
                && f.FollowedUserPublicId == followedUserId.Value)
            .Select(f => new FollowProjection(
                f.PublicId, f.UserPublicId!, f.TargetTypeId,
                f.DiscussionPublicId,
                f.SpacePublicId,
                f.FollowedUserPublicId,
                f.LevelId, f.CreatedAt))
            .FirstOrDefaultAsync(ct);
        return projection?.ToDomain();
    }

    public async Task<IEnumerable<UserId>> GetFollowersOfDiscussionAsync(DiscussionId discussionId, CancellationToken ct = default)
    {
        var discussion = await context.Discussions.FirstOrDefaultAsync(d => d.PublicId == discussionId.Value, ct);

        if (discussion is null) return [];

        var userIds = await databaseRepository.GetFollowerUserIdsOfDiscussionAsync(discussion.Id, ct);

        var userPublicIds = await context.Users
            .Where(u => userIds.Contains(u.Id))
            .Select(u => u.PublicId)
            .ToListAsync(ct);

        return userPublicIds.Select(UserId.From);
    }

    public async Task<IEnumerable<UserId>> GetFollowersOfSpaceAsync(SpaceId spaceId, CancellationToken ct = default)
    {
        var space = await context.Spaces.FirstOrDefaultAsync(s => s.PublicId == spaceId.Value, ct);

        if (space is null) return [];

        var userIds = await databaseRepository.GetFollowerUserIdsOfSpaceAsync(space.Id, ct);

        var userPublicIds = await context.Users
            .Where(u => userIds.Contains(u.Id))
            .Select(u => u.PublicId)
            .ToListAsync(ct);

        return userPublicIds.Select(UserId.From);
    }

    public async Task<IEnumerable<UserId>> GetFollowersOfUserAsync(UserId userId, CancellationToken ct = default)
    {
        var user = await context.Users.FirstOrDefaultAsync(u => u.PublicId == userId.Value, ct);

        if (user is null) return [];

        var followerIds = await databaseRepository.GetFollowerUserIdsOfUserAsync(user.Id, ct);

        var followerPublicIds = await context.Users
            .Where(u => followerIds.Contains(u.Id))
            .Select(u => u.PublicId)
            .ToListAsync(ct);

        return followerPublicIds.Select(UserId.From);
    }

    public async Task<int> GetFollowerCountOfUserAsync(UserId userId, CancellationToken ct = default)
    {
        var user = await context.Users.FirstOrDefaultAsync(u => u.PublicId == userId.Value, ct);

        if (user is null) return 0;

        return await databaseRepository.GetFollowerCountOfUserAsync(user.Id, ct);
    }

    public async Task<int> GetFollowingCountByUserAsync(UserId userId, CancellationToken ct = default)
    {
        return await context.UserFollows
            .Where(f => f.UserPublicId == userId.Value)
            .CountAsync(ct);
    }

    public async Task<IEnumerable<(UserId UserId, FollowLevel Level)>> GetFollowersOfSpaceWithLevelAsync(SpaceId spaceId, CancellationToken ct = default)
    {
        var space = await context.Spaces.FirstOrDefaultAsync(s => s.PublicId == spaceId.Value, ct);

        if (space is null) return [];

        var followersWithLevel = (await databaseRepository.GetFollowersOfSpaceWithLevelAsync(space.Id, ct)).ToList();

        var userIds = followersWithLevel.Select(f => f.UserId).ToList();
        var userIdToPublicId = await context.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.PublicId, ct);

        return followersWithLevel
            .Where(f => userIdToPublicId.ContainsKey(f.UserId))
            .Select(f => (
                UserId.From(userIdToPublicId[f.UserId]),
                ((FollowLevelEnum)f.LevelId).ToDomain()
            ));
    }

    public async Task<bool> IsFollowingDiscussionAsync(UserId userId, DiscussionId discussionId, CancellationToken ct = default)
    {
        var ids = await context.Users
            .Where(u => u.PublicId == userId.Value)
            .Join(context.Discussions.Where(d => d.PublicId == discussionId.Value),
                u => 1, d => 1, (u, d) => new { UserId = u.Id, DiscussionId = d.Id })
            .FirstOrDefaultAsync(ct);

        if (ids is null) return false;

        return await databaseRepository.IsFollowingDiscussionAsync(ids.UserId, ids.DiscussionId, ct);
    }

    public async Task<bool> IsFollowingSpaceAsync(UserId userId, SpaceId spaceId, CancellationToken ct = default)
    {
        var ids = await context.Users
            .Where(u => u.PublicId == userId.Value)
            .Join(context.Spaces.Where(s => s.PublicId == spaceId.Value),
                u => 1, d => 1, (u, s) => new { UserId = u.Id, SpaceId = s.Id })
            .FirstOrDefaultAsync(ct);

        if (ids is null) return false;

        return await databaseRepository.IsFollowingSpaceAsync(ids.UserId, ids.SpaceId, ct);
    }

    public async Task<bool> IsFollowingUserAsync(UserId userId, UserId followedUserId, CancellationToken ct = default)
    {
        var ids = await context.Users
            .Where(u => u.PublicId == userId.Value)
            .Join(context.Users.Where(u => u.PublicId == followedUserId.Value),
                u => 1, f => 1, (u, f) => new { UserId = u.Id, FollowedUserId = f.Id })
            .FirstOrDefaultAsync(ct);

        if (ids is null) return false;

        return await databaseRepository.IsFollowingUserAsync(ids.UserId, ids.FollowedUserId, ct);
    }

    public async Task<IEnumerable<SpaceId>> GetFollowedSpacesByUserAsync(UserId userId, CancellationToken ct = default)
    {
        var user = await context.Users.FirstOrDefaultAsync(u => u.PublicId == userId.Value, ct);

        if (user is null) return [];

        var publicIds = await databaseRepository.GetFollowedSpacePublicIdsByUserAsync(user.Id, ct);
        return publicIds.Select(SpaceId.From);
    }

    public async Task<IEnumerable<DiscussionId>> GetFollowedDiscussionsByUserAsync(UserId userId, CancellationToken ct = default)
    {
        var user = await context.Users.FirstOrDefaultAsync(u => u.PublicId == userId.Value, ct);

        if (user is null) return [];

        var publicIds = await databaseRepository.GetFollowedDiscussionPublicIdsByUserAsync(user.Id, ct);
        return publicIds.Select(DiscussionId.From);
    }

    public async Task<IEnumerable<(DiscussionId Id, DateTime FollowedAt)>> GetFollowedDiscussionsWithTimestampsAsync(UserId userId, CancellationToken ct = default)
    {
        var results = await context.UserFollows
            .Where(f => f.UserPublicId == userId.Value
                     && f.TargetTypeId == (int)FollowTargetTypeEnum.Discussion
                     && f.DiscussionPublicId != null)
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => new { f.DiscussionPublicId, f.CreatedAt })
            .ToListAsync(ct);
        return results
            .Where(x => x.DiscussionPublicId != null)
            .Select(x => (DiscussionId.From(x.DiscussionPublicId!), x.CreatedAt));
    }

    public async Task<IEnumerable<UserId>> GetFollowedUsersByUserAsync(UserId userId, CancellationToken ct = default)
    {
        var user = await context.Users.FirstOrDefaultAsync(u => u.PublicId == userId.Value, ct);

        if (user is null) return [];

        var publicIds = await databaseRepository.GetFollowedUserPublicIdsByUserAsync(user.Id, ct);
        return publicIds.Select(UserId.From);
    }

    public async Task AddAsync(Follow follow, CancellationToken ct = default)
    {
        var entity = follow.ToPersistence();

        // Resolve foreign keys (sequential — EF Core DbContext is not thread-safe)
        entity.UserId = await context.Users
            .Where(u => u.PublicId == follow.UserId.Value)
            .Select(u => (int?)u.Id)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"User with PublicId '{follow.UserId}' not found");
        entity.UserPublicId = follow.UserId.Value;

        if (follow.DiscussionId is not null)
        {
            entity.DiscussionId = await context.Discussions
                .Where(d => d.PublicId == follow.DiscussionId.Value)
                .Select(d => (int?)d.Id)
                .FirstOrDefaultAsync(ct)
                ?? throw new InvalidOperationException($"Discussion with PublicId '{follow.DiscussionId}' not found");
            entity.DiscussionPublicId = follow.DiscussionId.Value;
        }

        if (follow.SpaceId is not null)
        {
            entity.SpaceId = await context.Spaces
                .Where(s => s.PublicId == follow.SpaceId.Value)
                .Select(s => (int?)s.Id)
                .FirstOrDefaultAsync(ct)
                ?? throw new InvalidOperationException($"Space with PublicId '{follow.SpaceId}' not found");
            entity.SpacePublicId = follow.SpaceId.Value;
        }

        if (follow.FollowedUserId is not null)
        {
            entity.FollowedUserId = await context.Users
                .Where(u => u.PublicId == follow.FollowedUserId.Value)
                .Select(u => (int?)u.Id)
                .FirstOrDefaultAsync(ct)
                ?? throw new InvalidOperationException($"User with PublicId '{follow.FollowedUserId}' not found");
            entity.FollowedUserPublicId = follow.FollowedUserId.Value;
        }

        await databaseRepository.AddAsync(entity, ct);
        await databaseRepository.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Follow follow, CancellationToken ct = default)
    {
        var user = await context.Users.FirstOrDefaultAsync(u => u.PublicId == follow.UserId.Value, ct);

        if (user is null) return;

        Database.Entities.UserFollowDatabaseEntity? entity = null;

        if (follow.DiscussionId is not null)
        {
            var discussion = await context.Discussions.FirstOrDefaultAsync(d => d.PublicId == follow.DiscussionId.Value, ct);

            if (discussion is not null)
                entity = await databaseRepository.GetByUserAndDiscussionAsync(user.Id, discussion.Id, ct);
        }
        else if (follow.SpaceId is not null)
        {
            var space = await context.Spaces.FirstOrDefaultAsync(s => s.PublicId == follow.SpaceId.Value, ct);

            if (space is not null)
                entity = await databaseRepository.GetByUserAndSpaceAsync(user.Id, space.Id, ct);
        }
        else if (follow.FollowedUserId is not null)
        {
            var followedUser = await context.Users.FirstOrDefaultAsync(u => u.PublicId == follow.FollowedUserId.Value, ct);

            if (followedUser is not null)
                entity = await databaseRepository.GetByUserAndFollowedUserAsync(user.Id, followedUser.Id, ct);
        }

        if (entity is null) return;

        entity.LevelId = (int)follow.Level.ToShared();
        await databaseRepository.UpdateAsync(entity, ct);
        await databaseRepository.SaveChangesAsync(ct);
    }

    public async Task<bool> DeleteAsync(Follow follow, CancellationToken ct = default)
    {
        var user = await context.Users.FirstOrDefaultAsync(u => u.PublicId == follow.UserId.Value, ct);

        if (user is null) return false;

        Database.Entities.UserFollowDatabaseEntity? entity = null;

        if (follow.DiscussionId is not null)
        {
            var discussion = await context.Discussions.FirstOrDefaultAsync(d => d.PublicId == follow.DiscussionId.Value, ct);

            if (discussion is not null)
                entity = await databaseRepository.GetByUserAndDiscussionAsync(user.Id, discussion.Id, ct);
        }
        else if (follow.SpaceId is not null)
        {
            var space = await context.Spaces.FirstOrDefaultAsync(s => s.PublicId == follow.SpaceId.Value, ct);

            if (space is not null)
                entity = await databaseRepository.GetByUserAndSpaceAsync(user.Id, space.Id, ct);
        }
        else if (follow.FollowedUserId is not null)
        {
            var followedUser = await context.Users.FirstOrDefaultAsync(u => u.PublicId == follow.FollowedUserId.Value, ct);

            if (followedUser is not null)
                entity = await databaseRepository.GetByUserAndFollowedUserAsync(user.Id, followedUser.Id, ct);
        }

        if (entity is null) return false;

        await databaseRepository.DeleteAsync(entity, ct);
        await databaseRepository.SaveChangesAsync(ct);
        return true;
    }

    private record FollowProjection(
        string PublicId,
        string UserPublicId,
        int TargetTypeId,
        string? DiscussionPublicId,
        string? SpacePublicId,
        string? FollowedUserPublicId,
        int LevelId,
        DateTime CreatedAt)
    {
        public Follow ToDomain() => Follow.Rehydrate(
            FollowId.From(PublicId),
            UserId.From(UserPublicId),
            ((FollowTargetTypeEnum)TargetTypeId).ToDomain(),
            DiscussionPublicId is not null ? DiscussionId.From(DiscussionPublicId) : null,
            SpacePublicId is not null ? SpaceId.From(SpacePublicId) : null,
            FollowedUserPublicId is not null ? UserId.From(FollowedUserPublicId) : null,
            ((FollowLevelEnum)LevelId).ToDomain(),
            CreatedAt);
    }
}
