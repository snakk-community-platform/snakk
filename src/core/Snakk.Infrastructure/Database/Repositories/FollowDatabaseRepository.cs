namespace Snakk.Infrastructure.Database.Repositories;

using Microsoft.EntityFrameworkCore;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Shared.Enums;

public class FollowDatabaseRepository(SnakkDbContext context)
    : GenericDatabaseRepository<FollowDatabaseEntity>(context), IFollowDatabaseRepository
{
    // Compiled queries for high-frequency follow checks
    private static readonly Func<SnakkDbContext, int, int, Task<bool>> _isFollowingDiscussion
        = EF.CompileAsyncQuery(
            (SnakkDbContext ctx, int userId, int discussionId) =>
                ctx.Follows.Any(f => f.UserId == userId && f.DiscussionId == discussionId && f.TargetTypeId == (int)FollowTargetTypeEnum.Discussion));

    private static readonly Func<SnakkDbContext, int, int, Task<bool>> _isFollowingSpace
        = EF.CompileAsyncQuery(
            (SnakkDbContext ctx, int userId, int spaceId) =>
                ctx.Follows.Any(f => f.UserId == userId && f.SpaceId == spaceId && f.TargetTypeId == (int)FollowTargetTypeEnum.Space));

    private static readonly Func<SnakkDbContext, int, int, Task<bool>> _isFollowingUser
        = EF.CompileAsyncQuery(
            (SnakkDbContext ctx, int userId, int followedUserId) =>
                ctx.Follows.Any(f => f.UserId == userId && f.FollowedUserId == followedUserId && f.TargetTypeId == (int)FollowTargetTypeEnum.User));

    public async Task<FollowDatabaseEntity?> GetByUserAndDiscussionAsync(int userId, int discussionId)
    {
        return await _dbSet
            .FirstOrDefaultAsync(f => f.UserId == userId && f.DiscussionId == discussionId && f.TargetTypeId == (int)FollowTargetTypeEnum.Discussion);
    }

    public async Task<FollowDatabaseEntity?> GetByUserAndSpaceAsync(int userId, int spaceId)
    {
        return await _dbSet
            .FirstOrDefaultAsync(f => f.UserId == userId && f.SpaceId == spaceId && f.TargetTypeId == (int)FollowTargetTypeEnum.Space);
    }

    public async Task<FollowDatabaseEntity?> GetByUserAndFollowedUserAsync(int userId, int followedUserId)
    {
        return await _dbSet
            .FirstOrDefaultAsync(f => f.UserId == userId && f.FollowedUserId == followedUserId && f.TargetTypeId == (int)FollowTargetTypeEnum.User);
    }

    public async Task<IEnumerable<int>> GetFollowerUserIdsOfDiscussionAsync(int discussionId)
    {
        return await _dbSet
            .Where(f => f.DiscussionId == discussionId && f.TargetTypeId == (int)FollowTargetTypeEnum.Discussion)
            .Select(f => f.UserId)
            .ToListAsync();
    }

    public async Task<IEnumerable<int>> GetFollowerUserIdsOfSpaceAsync(int spaceId)
    {
        return await _dbSet
            .Where(f => f.SpaceId == spaceId && f.TargetTypeId == (int)FollowTargetTypeEnum.Space)
            .Select(f => f.UserId)
            .ToListAsync();
    }

    public async Task<IEnumerable<int>> GetFollowerUserIdsOfUserAsync(int userId)
    {
        return await _dbSet
            .Where(f => f.FollowedUserId == userId && f.TargetTypeId == (int)FollowTargetTypeEnum.User)
            .Select(f => f.UserId)
            .ToListAsync();
    }

    public async Task<int> GetFollowerCountOfUserAsync(int userId)
    {
        return await _dbSet
            .CountAsync(f => f.FollowedUserId == userId && f.TargetTypeId == (int)FollowTargetTypeEnum.User);
    }

    public async Task<IEnumerable<(int UserId, int LevelId)>> GetFollowersOfSpaceWithLevelAsync(int spaceId)
    {
        return await _dbSet
            .Where(f => f.SpaceId == spaceId && f.TargetTypeId == (int)FollowTargetTypeEnum.Space)
            .Select(f => new { f.UserId, f.LevelId })
            .ToListAsync()
            .ContinueWith(t => t.Result.Select(x => (x.UserId, x.LevelId)));
    }

    public async Task<bool> IsFollowingDiscussionAsync(int userId, int discussionId)
    {
        return await _isFollowingDiscussion(_context, userId, discussionId);
    }

    public async Task<bool> IsFollowingSpaceAsync(int userId, int spaceId)
    {
        return await _isFollowingSpace(_context, userId, spaceId);
    }

    public async Task<bool> IsFollowingUserAsync(int userId, int followedUserId)
    {
        return await _isFollowingUser(_context, userId, followedUserId);
    }

    public async Task<IEnumerable<string>> GetFollowedSpacePublicIdsByUserAsync(int userId)
    {
        return await _dbSet
            .Where(f => f.UserId == userId && f.TargetTypeId == (int)FollowTargetTypeEnum.Space && f.Space != null)
            .Select(f => f.Space.PublicId)
            .ToListAsync();
    }

    public async Task<IEnumerable<string>> GetFollowedDiscussionPublicIdsByUserAsync(int userId)
    {
        return await _dbSet
            .Where(f => f.UserId == userId && f.TargetTypeId == (int)FollowTargetTypeEnum.Discussion && f.Discussion != null)
            .Select(f => f.Discussion.PublicId)
            .ToListAsync();
    }

    public async Task<IEnumerable<string>> GetFollowedUserPublicIdsByUserAsync(int userId)
    {
        return await _dbSet
            .Where(f => f.UserId == userId && f.TargetTypeId == (int)FollowTargetTypeEnum.User && f.FollowedUser != null)
            .Select(f => f.FollowedUser.PublicId)
            .ToListAsync();
    }
}
