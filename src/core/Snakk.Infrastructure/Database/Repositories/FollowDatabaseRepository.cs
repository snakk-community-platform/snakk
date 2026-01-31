namespace Snakk.Infrastructure.Database.Repositories;

using Microsoft.EntityFrameworkCore;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;
using Snakk.Shared.Enums;

public class FollowDatabaseRepository(SnakkDbContext context)
    : GenericDatabaseRepository<FollowDatabaseEntity>(context), IFollowDatabaseRepository
{
    public override async Task<FollowDatabaseEntity?> GetByIdAsync(int id)
    {
        return await _dbSet
            .Include(f => f.User)
            .Include(f => f.Discussion)
            .Include(f => f.Space)
            .Include(f => f.FollowedUser)
            .FirstOrDefaultAsync(f => f.Id == id);
    }

    public override async Task<IEnumerable<FollowDatabaseEntity>> GetAllAsync()
    {
        return await _dbSet
            .Include(f => f.User)
            .Include(f => f.Discussion)
            .Include(f => f.Space)
            .Include(f => f.FollowedUser)
            .ToListAsync();
    }

    public async Task<FollowDatabaseEntity?> GetByUserAndDiscussionAsync(int userId, int discussionId)
    {
        return await _dbSet
            .Include(f => f.User)
            .Include(f => f.Discussion)
            .Include(f => f.Space)
            .Include(f => f.FollowedUser)
            .FirstOrDefaultAsync(f => f.UserId == userId && f.DiscussionId == discussionId && f.TargetTypeId == (int)FollowTargetTypeEnum.Discussion);
    }

    public async Task<FollowDatabaseEntity?> GetByUserAndSpaceAsync(int userId, int spaceId)
    {
        return await _dbSet
            .Include(f => f.User)
            .Include(f => f.Discussion)
            .Include(f => f.Space)
            .Include(f => f.FollowedUser)
            .FirstOrDefaultAsync(f => f.UserId == userId && f.SpaceId == spaceId && f.TargetTypeId == (int)FollowTargetTypeEnum.Space);
    }

    public async Task<FollowDatabaseEntity?> GetByUserAndFollowedUserAsync(int userId, int followedUserId)
    {
        return await _dbSet
            .Include(f => f.User)
            .Include(f => f.Discussion)
            .Include(f => f.Space)
            .Include(f => f.FollowedUser)
            .FirstOrDefaultAsync(f => f.UserId == userId && f.FollowedUserId == followedUserId && f.TargetTypeId == (int)FollowTargetTypeEnum.User);
    }

    public async Task<IEnumerable<int>> GetFollowerUserIdsOfDiscussionAsync(int discussionId)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(f => f.DiscussionId == discussionId && f.TargetTypeId == (int)FollowTargetTypeEnum.Discussion)
            .Select(f => f.UserId)
            .ToListAsync();
    }

    public async Task<IEnumerable<int>> GetFollowerUserIdsOfSpaceAsync(int spaceId)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(f => f.SpaceId == spaceId && f.TargetTypeId == (int)FollowTargetTypeEnum.Space)
            .Select(f => f.UserId)
            .ToListAsync();
    }

    public async Task<IEnumerable<int>> GetFollowerUserIdsOfUserAsync(int userId)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(f => f.FollowedUserId == userId && f.TargetTypeId == (int)FollowTargetTypeEnum.User)
            .Select(f => f.UserId)
            .ToListAsync();
    }

    public async Task<int> GetFollowerCountOfUserAsync(int userId)
    {
        return await _dbSet
            .AsNoTracking()
            .CountAsync(f => f.FollowedUserId == userId && f.TargetTypeId == (int)FollowTargetTypeEnum.User);
    }

    public async Task<IEnumerable<(int UserId, int LevelId)>> GetFollowersOfSpaceWithLevelAsync(int spaceId)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(f => f.SpaceId == spaceId && f.TargetTypeId == (int)FollowTargetTypeEnum.Space)
            .Select(f => new { f.UserId, f.LevelId })
            .ToListAsync()
            .ContinueWith(t => t.Result.Select(x => (x.UserId, x.LevelId)));
    }

    public async Task<bool> IsFollowingDiscussionAsync(int userId, int discussionId)
    {
        return await _dbSet
            .AsNoTracking()
            .AnyAsync(f => f.UserId == userId && f.DiscussionId == discussionId && f.TargetTypeId == (int)FollowTargetTypeEnum.Discussion);
    }

    public async Task<bool> IsFollowingSpaceAsync(int userId, int spaceId)
    {
        return await _dbSet
            .AsNoTracking()
            .AnyAsync(f => f.UserId == userId && f.SpaceId == spaceId && f.TargetTypeId == (int)FollowTargetTypeEnum.Space);
    }

    public async Task<bool> IsFollowingUserAsync(int userId, int followedUserId)
    {
        return await _dbSet
            .AsNoTracking()
            .AnyAsync(f => f.UserId == userId && f.FollowedUserId == followedUserId && f.TargetTypeId == (int)FollowTargetTypeEnum.User);
    }

    public async Task<IEnumerable<string>> GetFollowedSpacePublicIdsByUserAsync(int userId)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(f => f.UserId == userId && f.TargetTypeId == (int)FollowTargetTypeEnum.Space && f.Space != null)
            .Select(f => f.Space.PublicId)
            .ToListAsync();
    }

    public async Task<IEnumerable<string>> GetFollowedDiscussionPublicIdsByUserAsync(int userId)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(f => f.UserId == userId && f.TargetTypeId == (int)FollowTargetTypeEnum.Discussion && f.Discussion != null)
            .Select(f => f.Discussion.PublicId)
            .ToListAsync();
    }

    public async Task<IEnumerable<string>> GetFollowedUserPublicIdsByUserAsync(int userId)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(f => f.UserId == userId && f.TargetTypeId == (int)FollowTargetTypeEnum.User && f.FollowedUser != null)
            .Select(f => f.FollowedUser.PublicId)
            .ToListAsync();
    }
}
