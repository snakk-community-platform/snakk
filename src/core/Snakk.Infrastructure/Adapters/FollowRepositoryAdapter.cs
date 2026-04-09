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
    public async Task<Follow?> GetByUserAndDiscussionAsync(UserId userId, DiscussionId discussionId)
    {
        var projection = await context.UserFollows
            .Where(f =>
                f.User.PublicId == userId.Value
                && f.Discussion != null
                && f.Discussion.PublicId == discussionId.Value)
            .Select(f => new FollowProjection(
                f.PublicId, f.User.PublicId, f.TargetTypeId,
                f.Discussion != null ? f.Discussion.PublicId : null,
                f.Space != null ? f.Space.PublicId : null,
                f.FollowedUser != null ? f.FollowedUser.PublicId : null,
                f.LevelId, f.CreatedAt))
            .FirstOrDefaultAsync();
        return projection?.ToDomain();
    }

    public async Task<Follow?> GetByUserAndSpaceAsync(UserId userId, SpaceId spaceId)
    {
        var projection = await context.UserFollows
            .Where(f =>
                f.User.PublicId == userId.Value
                && f.Space != null
                && f.Space.PublicId == spaceId.Value)
            .Select(f => new FollowProjection(
                f.PublicId, f.User.PublicId, f.TargetTypeId,
                f.Discussion != null ? f.Discussion.PublicId : null,
                f.Space != null ? f.Space.PublicId : null,
                f.FollowedUser != null ? f.FollowedUser.PublicId : null,
                f.LevelId, f.CreatedAt))
            .FirstOrDefaultAsync();
        return projection?.ToDomain();
    }

    public async Task<Follow?> GetByUserAndFollowedUserAsync(UserId userId, UserId followedUserId)
    {
        var projection = await context.UserFollows
            .Where(f =>
                f.User.PublicId == userId.Value
                && f.FollowedUser != null
                && f.FollowedUser.PublicId == followedUserId.Value)
            .Select(f => new FollowProjection(
                f.PublicId, f.User.PublicId, f.TargetTypeId,
                f.Discussion != null ? f.Discussion.PublicId : null,
                f.Space != null ? f.Space.PublicId : null,
                f.FollowedUser != null ? f.FollowedUser.PublicId : null,
                f.LevelId, f.CreatedAt))
            .FirstOrDefaultAsync();
        return projection?.ToDomain();
    }

    public async Task<IEnumerable<UserId>> GetFollowersOfDiscussionAsync(DiscussionId discussionId)
    {
        var discussion = await context.Discussions.FirstOrDefaultAsync(d => d.PublicId == discussionId.Value);

        if (discussion is null) return [];

        var userIds = await databaseRepository.GetFollowerUserIdsOfDiscussionAsync(discussion.Id);

        var userPublicIds = await context.Users
            .Where(u => userIds.Contains(u.Id))
            .Select(u => u.PublicId)
            .ToListAsync();

        return userPublicIds.Select(UserId.From);
    }

    public async Task<IEnumerable<UserId>> GetFollowersOfSpaceAsync(SpaceId spaceId)
    {
        var space = await context.Spaces.FirstOrDefaultAsync(s => s.PublicId == spaceId.Value);

        if (space is null) return [];

        var userIds = await databaseRepository.GetFollowerUserIdsOfSpaceAsync(space.Id);

        var userPublicIds = await context.Users
            .Where(u => userIds.Contains(u.Id))
            .Select(u => u.PublicId)
            .ToListAsync();

        return userPublicIds.Select(UserId.From);
    }

    public async Task<IEnumerable<UserId>> GetFollowersOfUserAsync(UserId userId)
    {
        var user = await context.Users.FirstOrDefaultAsync(u => u.PublicId == userId.Value);

        if (user is null) return [];

        var followerIds = await databaseRepository.GetFollowerUserIdsOfUserAsync(user.Id);

        var followerPublicIds = await context.Users
            .Where(u => followerIds.Contains(u.Id))
            .Select(u => u.PublicId)
            .ToListAsync();

        return followerPublicIds.Select(UserId.From);
    }

    public async Task<int> GetFollowerCountOfUserAsync(UserId userId)
    {
        var user = await context.Users.FirstOrDefaultAsync(u => u.PublicId == userId.Value);

        if (user is null) return 0;

        return await databaseRepository.GetFollowerCountOfUserAsync(user.Id);
    }

    public async Task<IEnumerable<(UserId UserId, FollowLevel Level)>> GetFollowersOfSpaceWithLevelAsync(SpaceId spaceId)
    {
        var space = await context.Spaces.FirstOrDefaultAsync(s => s.PublicId == spaceId.Value);

        if (space is null) return [];

        var followersWithLevel = (await databaseRepository.GetFollowersOfSpaceWithLevelAsync(space.Id)).ToList();

        var userIds = followersWithLevel.Select(f => f.UserId).ToList();
        var userIdToPublicId = await context.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.PublicId);

        return followersWithLevel
            .Where(f => userIdToPublicId.ContainsKey(f.UserId))
            .Select(f => (
                UserId.From(userIdToPublicId[f.UserId]),
                ((FollowLevelEnum)f.LevelId).ToDomain()
            ));
    }

    public async Task<bool> IsFollowingDiscussionAsync(UserId userId, DiscussionId discussionId)
    {
        var ids = await context.Users
            .Where(u => u.PublicId == userId.Value)
            .Join(context.Discussions.Where(d => d.PublicId == discussionId.Value),
                u => 1, d => 1, (u, d) => new { UserId = u.Id, DiscussionId = d.Id })
            .FirstOrDefaultAsync();

        if (ids is null) return false;

        return await databaseRepository.IsFollowingDiscussionAsync(ids.UserId, ids.DiscussionId);
    }

    public async Task<bool> IsFollowingSpaceAsync(UserId userId, SpaceId spaceId)
    {
        var ids = await context.Users
            .Where(u => u.PublicId == userId.Value)
            .Join(context.Spaces.Where(s => s.PublicId == spaceId.Value),
                u => 1, d => 1, (u, s) => new { UserId = u.Id, SpaceId = s.Id })
            .FirstOrDefaultAsync();

        if (ids is null) return false;

        return await databaseRepository.IsFollowingSpaceAsync(ids.UserId, ids.SpaceId);
    }

    public async Task<bool> IsFollowingUserAsync(UserId userId, UserId followedUserId)
    {
        var ids = await context.Users
            .Where(u => u.PublicId == userId.Value)
            .Join(context.Users.Where(u => u.PublicId == followedUserId.Value),
                u => 1, f => 1, (u, f) => new { UserId = u.Id, FollowedUserId = f.Id })
            .FirstOrDefaultAsync();

        if (ids is null) return false;

        return await databaseRepository.IsFollowingUserAsync(ids.UserId, ids.FollowedUserId);
    }

    public async Task<IEnumerable<SpaceId>> GetFollowedSpacesByUserAsync(UserId userId)
    {
        var user = await context.Users.FirstOrDefaultAsync(u => u.PublicId == userId.Value);

        if (user is null) return [];

        var publicIds = await databaseRepository.GetFollowedSpacePublicIdsByUserAsync(user.Id);
        return publicIds.Select(SpaceId.From);
    }

    public async Task<IEnumerable<DiscussionId>> GetFollowedDiscussionsByUserAsync(UserId userId)
    {
        var user = await context.Users.FirstOrDefaultAsync(u => u.PublicId == userId.Value);

        if (user is null) return [];

        var publicIds = await databaseRepository.GetFollowedDiscussionPublicIdsByUserAsync(user.Id);
        return publicIds.Select(DiscussionId.From);
    }

    public async Task<IEnumerable<UserId>> GetFollowedUsersByUserAsync(UserId userId)
    {
        var user = await context.Users.FirstOrDefaultAsync(u => u.PublicId == userId.Value);

        if (user is null) return [];

        var publicIds = await databaseRepository.GetFollowedUserPublicIdsByUserAsync(user.Id);
        return publicIds.Select(UserId.From);
    }

    public async Task AddAsync(Follow follow)
    {
        var entity = follow.ToPersistence();

        // Resolve foreign keys (sequential — EF Core DbContext is not thread-safe)
        entity.UserId = await context.Users
            .Where(u => u.PublicId == follow.UserId.Value)
            .Select(u => (int?)u.Id)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException($"User with PublicId '{follow.UserId}' not found");

        if (follow.DiscussionId is not null)
        {
            entity.DiscussionId = await context.Discussions
                .Where(d => d.PublicId == follow.DiscussionId.Value)
                .Select(d => (int?)d.Id)
                .FirstOrDefaultAsync()
                ?? throw new InvalidOperationException($"Discussion with PublicId '{follow.DiscussionId}' not found");
        }

        if (follow.SpaceId is not null)
        {
            entity.SpaceId = await context.Spaces
                .Where(s => s.PublicId == follow.SpaceId.Value)
                .Select(s => (int?)s.Id)
                .FirstOrDefaultAsync()
                ?? throw new InvalidOperationException($"Space with PublicId '{follow.SpaceId}' not found");
        }

        if (follow.FollowedUserId is not null)
        {
            entity.FollowedUserId = await context.Users
                .Where(u => u.PublicId == follow.FollowedUserId.Value)
                .Select(u => (int?)u.Id)
                .FirstOrDefaultAsync()
                ?? throw new InvalidOperationException($"User with PublicId '{follow.FollowedUserId}' not found");
        }

        await databaseRepository.AddAsync(entity);
        await databaseRepository.SaveChangesAsync();
    }

    public async Task UpdateAsync(Follow follow)
    {
        var user = await context.Users.FirstOrDefaultAsync(u => u.PublicId == follow.UserId.Value);

        if (user is null) return;

        Database.Entities.UserFollowDatabaseEntity? entity = null;

        if (follow.DiscussionId is not null)
        {
            var discussion = await context.Discussions.FirstOrDefaultAsync(d => d.PublicId == follow.DiscussionId.Value);

            if (discussion is not null)
                entity = await databaseRepository.GetByUserAndDiscussionAsync(user.Id, discussion.Id);
        }
        else if (follow.SpaceId is not null)
        {
            var space = await context.Spaces.FirstOrDefaultAsync(s => s.PublicId == follow.SpaceId.Value);

            if (space is not null)
                entity = await databaseRepository.GetByUserAndSpaceAsync(user.Id, space.Id);
        }
        else if (follow.FollowedUserId is not null)
        {
            var followedUser = await context.Users.FirstOrDefaultAsync(u => u.PublicId == follow.FollowedUserId.Value);

            if (followedUser is not null)
                entity = await databaseRepository.GetByUserAndFollowedUserAsync(user.Id, followedUser.Id);
        }

        if (entity is null) return;

        entity.LevelId = (int)follow.Level.ToShared();
        await databaseRepository.UpdateAsync(entity);
        await databaseRepository.SaveChangesAsync();
    }

    public async Task DeleteAsync(Follow follow)
    {
        var user = await context.Users.FirstOrDefaultAsync(u => u.PublicId == follow.UserId.Value);

        if (user is null) return;

        Database.Entities.UserFollowDatabaseEntity? entity = null;

        if (follow.DiscussionId is not null)
        {
            var discussion = await context.Discussions.FirstOrDefaultAsync(d => d.PublicId == follow.DiscussionId.Value);

            if (discussion is not null)
                entity = await databaseRepository.GetByUserAndDiscussionAsync(user.Id, discussion.Id);
        }
        else if (follow.SpaceId is not null)
        {
            var space = await context.Spaces.FirstOrDefaultAsync(s => s.PublicId == follow.SpaceId.Value);

            if (space is not null)
                entity = await databaseRepository.GetByUserAndSpaceAsync(user.Id, space.Id);
        }
        else if (follow.FollowedUserId is not null)
        {
            var followedUser = await context.Users.FirstOrDefaultAsync(u => u.PublicId == follow.FollowedUserId.Value);

            if (followedUser is not null)
                entity = await databaseRepository.GetByUserAndFollowedUserAsync(user.Id, followedUser.Id);
        }

        if (entity is null) return;

        await databaseRepository.DeleteAsync(entity);
        await databaseRepository.SaveChangesAsync();
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
