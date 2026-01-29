namespace Snakk.Infrastructure.Database.Repositories;

using Microsoft.EntityFrameworkCore;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;

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
            .FirstOrDefaultAsync(f => f.UserId == userId && f.DiscussionId == discussionId && f.TargetType == "Discussion");
    }

    public async Task<FollowDatabaseEntity?> GetByUserAndSpaceAsync(int userId, int spaceId)
    {
        return await _dbSet
            .Include(f => f.User)
            .Include(f => f.Discussion)
            .Include(f => f.Space)
            .Include(f => f.FollowedUser)
            .FirstOrDefaultAsync(f => f.UserId == userId && f.SpaceId == spaceId && f.TargetType == "Space");
    }

    public async Task<FollowDatabaseEntity?> GetByUserAndFollowedUserAsync(int userId, int followedUserId)
    {
        return await _dbSet
            .Include(f => f.User)
            .Include(f => f.Discussion)
            .Include(f => f.Space)
            .Include(f => f.FollowedUser)
            .FirstOrDefaultAsync(f => f.UserId == userId && f.FollowedUserId == followedUserId && f.TargetType == "User");
    }

    public async Task<IEnumerable<int>> GetFollowerUserIdsOfDiscussionAsync(int discussionId)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(f => f.DiscussionId == discussionId && f.TargetType == "Discussion")
            .Select(f => f.UserId)
            .ToListAsync();
    }

    public async Task<IEnumerable<int>> GetFollowerUserIdsOfSpaceAsync(int spaceId)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(f => f.SpaceId == spaceId && f.TargetType == "Space")
            .Select(f => f.UserId)
            .ToListAsync();
    }

    public async Task<IEnumerable<int>> GetFollowerUserIdsOfUserAsync(int userId)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(f => f.FollowedUserId == userId && f.TargetType == "User")
            .Select(f => f.UserId)
            .ToListAsync();
    }

    public async Task<int> GetFollowerCountOfUserAsync(int userId)
    {
        return await _dbSet
            .AsNoTracking()
            .CountAsync(f => f.FollowedUserId == userId && f.TargetType == "User");
    }

    public async Task<IEnumerable<(int UserId, string Level)>> GetFollowersOfSpaceWithLevelAsync(int spaceId)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(f => f.SpaceId == spaceId && f.TargetType == "Space")
            .Select(f => new { f.UserId, f.Level })
            .ToListAsync()
            .ContinueWith(t => t.Result.Select(x => (x.UserId, x.Level)));
    }

    public async Task<bool> IsFollowingDiscussionAsync(int userId, int discussionId)
    {
        return await _dbSet
            .AsNoTracking()
            .AnyAsync(f => f.UserId == userId && f.DiscussionId == discussionId && f.TargetType == "Discussion");
    }

    public async Task<bool> IsFollowingSpaceAsync(int userId, int spaceId)
    {
        return await _dbSet
            .AsNoTracking()
            .AnyAsync(f => f.UserId == userId && f.SpaceId == spaceId && f.TargetType == "Space");
    }

    public async Task<bool> IsFollowingUserAsync(int userId, int followedUserId)
    {
        return await _dbSet
            .AsNoTracking()
            .AnyAsync(f => f.UserId == userId && f.FollowedUserId == followedUserId && f.TargetType == "User");
    }

    public async Task<IEnumerable<string>> GetFollowedSpacePublicIdsByUserAsync(int userId)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(f => f.UserId == userId && f.TargetType == "Space" && f.Space != null)
            .Select(f => f.Space.PublicId)
            .ToListAsync();
    }

    public async Task<IEnumerable<string>> GetFollowedDiscussionPublicIdsByUserAsync(int userId)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(f => f.UserId == userId && f.TargetType == "Discussion" && f.Discussion != null)
            .Select(f => f.Discussion.PublicId)
            .ToListAsync();
    }

    public async Task<IEnumerable<string>> GetFollowedUserPublicIdsByUserAsync(int userId)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(f => f.UserId == userId && f.TargetType == "User" && f.FollowedUser != null)
            .Select(f => f.FollowedUser.PublicId)
            .ToListAsync();
    }
}
