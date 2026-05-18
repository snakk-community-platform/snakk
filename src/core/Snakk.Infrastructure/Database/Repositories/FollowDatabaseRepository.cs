namespace Snakk.Infrastructure.Database.Repositories;

using Microsoft.EntityFrameworkCore;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Shared.Enums;

public class FollowDatabaseRepository(SnakkDbContext context)
    : GenericDatabaseRepository<UserFollowDatabaseEntity>(context), IFollowDatabaseRepository
{
    // Compiled queries for high-frequency follow checks
    private static readonly Func<SnakkDbContext, int, int, Task<bool>> _isFollowingDiscussion
        = EF.CompileAsyncQuery(
            (SnakkDbContext ctx, int userId, int discussionId) =>
                ctx.UserFollows.Any(f =>
                    f.UserId == userId
                    && f.DiscussionId == discussionId
                    && f.TargetTypeId == (int)FollowTargetTypeEnum.Discussion));

    private static readonly Func<SnakkDbContext, int, int, Task<bool>> _isFollowingSpace
        = EF.CompileAsyncQuery(
            (SnakkDbContext ctx, int userId, int spaceId) =>
                ctx.UserFollows.Any(f =>
                    f.UserId == userId
                    && f.SpaceId == spaceId
                    && f.TargetTypeId == (int)FollowTargetTypeEnum.Space));

    private static readonly Func<SnakkDbContext, int, int, Task<bool>> _isFollowingUser
        = EF.CompileAsyncQuery(
            (SnakkDbContext ctx, int userId, int followedUserId) =>
                ctx.UserFollows.Any(f =>
                    f.UserId == userId
                    && f.FollowedUserId == followedUserId
                    && f.TargetTypeId == (int)FollowTargetTypeEnum.User));

    public async Task<UserFollowDatabaseEntity?> GetByUserAndDiscussionAsync(
        int userId,
        int discussionId,
        CancellationToken ct = default) => await _dbSet
        .FirstOrDefaultAsync(f =>
            f.UserId == userId
            && f.DiscussionId == discussionId
            && f.TargetTypeId == (int)FollowTargetTypeEnum.Discussion, ct);

    public async Task<UserFollowDatabaseEntity?> GetByUserAndSpaceAsync(
        int userId,
        int spaceId,
        CancellationToken ct = default) => await _dbSet
        .FirstOrDefaultAsync(f =>
            f.UserId == userId
            && f.SpaceId == spaceId
            && f.TargetTypeId == (int)FollowTargetTypeEnum.Space, ct);

    public async Task<UserFollowDatabaseEntity?> GetByUserAndFollowedUserAsync(
        int userId,
        int followedUserId,
        CancellationToken ct = default) => await _dbSet
        .FirstOrDefaultAsync(f =>
            f.UserId == userId
            && f.FollowedUserId == followedUserId
            && f.TargetTypeId == (int)FollowTargetTypeEnum.User, ct);

    public async Task<IEnumerable<int>> GetFollowerUserIdsOfDiscussionAsync(int discussionId, CancellationToken ct = default) => await _dbSet
        .Where(f =>
            f.DiscussionId == discussionId
            && f.TargetTypeId == (int)FollowTargetTypeEnum.Discussion)
        .Select(f => f.UserId)
        .Take(5000)
        .ToListAsync(ct);

    public async Task<IEnumerable<int>> GetFollowerUserIdsOfSpaceAsync(int spaceId, CancellationToken ct = default) => await _dbSet
        .Where(f =>
            f.SpaceId == spaceId
            && f.TargetTypeId == (int)FollowTargetTypeEnum.Space)
        .Select(f => f.UserId)
        .Take(5000)
        .ToListAsync(ct);

    public async Task<IEnumerable<int>> GetFollowerUserIdsOfUserAsync(int userId, CancellationToken ct = default) => await _dbSet
        .Where(f =>
            f.FollowedUserId == userId
            && f.TargetTypeId == (int)FollowTargetTypeEnum.User)
        .Select(f => f.UserId)
        .Take(5000)
        .ToListAsync(ct);

    public async Task<int> GetFollowerCountOfUserAsync(int userId, CancellationToken ct = default) => await _dbSet
        .CountAsync(f =>
            f.FollowedUserId == userId
            && f.TargetTypeId == (int)FollowTargetTypeEnum.User, ct);

    public async Task<IEnumerable<(int UserId, int LevelId)>> GetFollowersOfSpaceWithLevelAsync(int spaceId, CancellationToken ct = default)
    {
        var results = await _dbSet
            .Where(f =>
                f.SpaceId == spaceId
                && f.TargetTypeId == (int)FollowTargetTypeEnum.Space)
            .Select(f => new {
                f.UserId,
                f.LevelId })
            .ToListAsync(ct);

        return results.Select(x => (x.UserId, x.LevelId));
    }

    public async Task<bool> IsFollowingDiscussionAsync(int userId, int discussionId, CancellationToken ct = default) =>
        await _isFollowingDiscussion(_context, userId, discussionId);

    public async Task<bool> IsFollowingSpaceAsync(int userId, int spaceId, CancellationToken ct = default) =>
        await _isFollowingSpace(_context, userId, spaceId);

    public async Task<bool> IsFollowingUserAsync(int userId, int followedUserId, CancellationToken ct = default) =>
        await _isFollowingUser(_context, userId, followedUserId);

    public async Task<IEnumerable<string>> GetFollowedSpacePublicIdsByUserAsync(int userId, CancellationToken ct = default) => await _dbSet
        .Where(f =>
            f.UserId == userId
            && f.TargetTypeId == (int)FollowTargetTypeEnum.Space
            && f.Space != null)
        .Select(f => f.Space!.PublicId)
        .ToListAsync(ct);

    public async Task<IEnumerable<string>> GetFollowedDiscussionPublicIdsByUserAsync(int userId, CancellationToken ct = default) => await _dbSet
        .Where(f =>
            f.UserId == userId
            && f.TargetTypeId == (int)FollowTargetTypeEnum.Discussion
            && f.Discussion != null)
        .Select(f => f.Discussion!.PublicId)
        .ToListAsync(ct);

    public async Task<IEnumerable<string>> GetFollowedUserPublicIdsByUserAsync(int userId, CancellationToken ct = default) => await _dbSet
        .Where(f =>
            f.UserId == userId
            && f.TargetTypeId == (int)FollowTargetTypeEnum.User
            && f.FollowedUser != null)
        .Select(f => f.FollowedUser!.PublicId)
        .ToListAsync(ct);
}
