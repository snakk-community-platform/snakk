namespace Snakk.Infrastructure.Adapters;

using Microsoft.EntityFrameworkCore;
using Snakk.Domain.Entities;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Repositories;
using Snakk.Infrastructure.Mappers;

public class FollowRepositoryAdapter(
    IFollowDatabaseRepository databaseRepository,
    SnakkDbContext context) : IFollowRepository
{
    private readonly IFollowDatabaseRepository _databaseRepository = databaseRepository;
    private readonly SnakkDbContext _context = context;

    public async Task<Follow?> GetByUserAndDiscussionAsync(UserId userId, DiscussionId discussionId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.PublicId == userId.Value);
        var discussion = await _context.Discussions.FirstOrDefaultAsync(d => d.PublicId == discussionId.Value);

        if (user == null || discussion == null) return null;

        var entity = await _databaseRepository.GetByUserAndDiscussionAsync(user.Id, discussion.Id);
        if (entity == null) return null;

        entity.User = user;
        entity.Discussion = discussion;

        return entity.FromPersistence();
    }

    public async Task<Follow?> GetByUserAndSpaceAsync(UserId userId, SpaceId spaceId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.PublicId == userId.Value);
        var space = await _context.Spaces.FirstOrDefaultAsync(s => s.PublicId == spaceId.Value);

        if (user == null || space == null) return null;

        var entity = await _databaseRepository.GetByUserAndSpaceAsync(user.Id, space.Id);
        if (entity == null) return null;

        entity.User = user;
        entity.Space = space;

        return entity.FromPersistence();
    }

    public async Task<Follow?> GetByUserAndFollowedUserAsync(UserId userId, UserId followedUserId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.PublicId == userId.Value);
        var followedUser = await _context.Users.FirstOrDefaultAsync(u => u.PublicId == followedUserId.Value);

        if (user == null || followedUser == null) return null;

        var entity = await _databaseRepository.GetByUserAndFollowedUserAsync(user.Id, followedUser.Id);
        if (entity == null) return null;

        entity.User = user;
        entity.FollowedUser = followedUser;

        return entity.FromPersistence();
    }

    public async Task<IEnumerable<UserId>> GetFollowersOfDiscussionAsync(DiscussionId discussionId)
    {
        var discussion = await _context.Discussions.FirstOrDefaultAsync(d => d.PublicId == discussionId.Value);
        if (discussion == null) return [];

        var userIds = await _databaseRepository.GetFollowerUserIdsOfDiscussionAsync(discussion.Id);

        var userPublicIds = await _context.Users
            .Where(u => userIds.Contains(u.Id))
            .Select(u => u.PublicId)
            .ToListAsync();

        return userPublicIds.Select(UserId.From);
    }

    public async Task<IEnumerable<UserId>> GetFollowersOfSpaceAsync(SpaceId spaceId)
    {
        var space = await _context.Spaces.FirstOrDefaultAsync(s => s.PublicId == spaceId.Value);
        if (space == null) return [];

        var userIds = await _databaseRepository.GetFollowerUserIdsOfSpaceAsync(space.Id);

        var userPublicIds = await _context.Users
            .Where(u => userIds.Contains(u.Id))
            .Select(u => u.PublicId)
            .ToListAsync();

        return userPublicIds.Select(UserId.From);
    }

    public async Task<IEnumerable<UserId>> GetFollowersOfUserAsync(UserId userId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.PublicId == userId.Value);
        if (user == null) return [];

        var followerIds = await _databaseRepository.GetFollowerUserIdsOfUserAsync(user.Id);

        var followerPublicIds = await _context.Users
            .Where(u => followerIds.Contains(u.Id))
            .Select(u => u.PublicId)
            .ToListAsync();

        return followerPublicIds.Select(UserId.From);
    }

    public async Task<int> GetFollowerCountOfUserAsync(UserId userId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.PublicId == userId.Value);
        if (user == null) return 0;

        return await _databaseRepository.GetFollowerCountOfUserAsync(user.Id);
    }

    public async Task<IEnumerable<(UserId UserId, FollowLevel Level)>> GetFollowersOfSpaceWithLevelAsync(SpaceId spaceId)
    {
        var space = await _context.Spaces.FirstOrDefaultAsync(s => s.PublicId == spaceId.Value);
        if (space == null) return [];

        var followersWithLevel = (await _databaseRepository.GetFollowersOfSpaceWithLevelAsync(space.Id)).ToList();

        var userIdToPublicId = await _context.Users
            .Where(u => followersWithLevel.Select(f => f.UserId).Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.PublicId);

        return followersWithLevel
            .Where(f => userIdToPublicId.ContainsKey(f.UserId))
            .Select(f => (
                UserId.From(userIdToPublicId[f.UserId]),
                Enum.TryParse<FollowLevel>(f.Level, out var level) ? level : FollowLevel.DiscussionsOnly
            ));
    }

    public async Task<bool> IsFollowingDiscussionAsync(UserId userId, DiscussionId discussionId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.PublicId == userId.Value);
        var discussion = await _context.Discussions.FirstOrDefaultAsync(d => d.PublicId == discussionId.Value);

        if (user == null || discussion == null) return false;

        return await _databaseRepository.IsFollowingDiscussionAsync(user.Id, discussion.Id);
    }

    public async Task<bool> IsFollowingSpaceAsync(UserId userId, SpaceId spaceId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.PublicId == userId.Value);
        var space = await _context.Spaces.FirstOrDefaultAsync(s => s.PublicId == spaceId.Value);

        if (user == null || space == null) return false;

        return await _databaseRepository.IsFollowingSpaceAsync(user.Id, space.Id);
    }

    public async Task<bool> IsFollowingUserAsync(UserId userId, UserId followedUserId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.PublicId == userId.Value);
        var followedUser = await _context.Users.FirstOrDefaultAsync(u => u.PublicId == followedUserId.Value);

        if (user == null || followedUser == null) return false;

        return await _databaseRepository.IsFollowingUserAsync(user.Id, followedUser.Id);
    }

    public async Task<IEnumerable<SpaceId>> GetFollowedSpacesByUserAsync(UserId userId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.PublicId == userId.Value);
        if (user == null) return [];

        var publicIds = await _databaseRepository.GetFollowedSpacePublicIdsByUserAsync(user.Id);
        return publicIds.Select(SpaceId.From);
    }

    public async Task<IEnumerable<DiscussionId>> GetFollowedDiscussionsByUserAsync(UserId userId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.PublicId == userId.Value);
        if (user == null) return [];

        var publicIds = await _databaseRepository.GetFollowedDiscussionPublicIdsByUserAsync(user.Id);
        return publicIds.Select(DiscussionId.From);
    }

    public async Task<IEnumerable<UserId>> GetFollowedUsersByUserAsync(UserId userId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.PublicId == userId.Value);
        if (user == null) return [];

        var publicIds = await _databaseRepository.GetFollowedUserPublicIdsByUserAsync(user.Id);
        return publicIds.Select(UserId.From);
    }

    public async Task AddAsync(Follow follow)
    {
        var entity = follow.ToPersistence();

        var user = await _context.Users.FirstOrDefaultAsync(u => u.PublicId == follow.UserId.Value);
        if (user == null)
            throw new InvalidOperationException($"User with PublicId '{follow.UserId}' not found");

        entity.UserId = user.Id;

        if (follow.DiscussionId != null)
        {
            var discussion = await _context.Discussions.FirstOrDefaultAsync(d => d.PublicId == follow.DiscussionId.Value);
            if (discussion == null)
                throw new InvalidOperationException($"Discussion with PublicId '{follow.DiscussionId}' not found");
            entity.DiscussionId = discussion.Id;
        }

        if (follow.SpaceId != null)
        {
            var space = await _context.Spaces.FirstOrDefaultAsync(s => s.PublicId == follow.SpaceId.Value);
            if (space == null)
                throw new InvalidOperationException($"Space with PublicId '{follow.SpaceId}' not found");
            entity.SpaceId = space.Id;
        }

        if (follow.FollowedUserId != null)
        {
            var followedUser = await _context.Users.FirstOrDefaultAsync(u => u.PublicId == follow.FollowedUserId.Value);
            if (followedUser == null)
                throw new InvalidOperationException($"User with PublicId '{follow.FollowedUserId}' not found");
            entity.FollowedUserId = followedUser.Id;
        }

        await _databaseRepository.AddAsync(entity);
        await _databaseRepository.SaveChangesAsync();
    }

    public async Task UpdateAsync(Follow follow)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.PublicId == follow.UserId.Value);
        if (user == null) return;

        Database.Entities.FollowDatabaseEntity? entity = null;

        if (follow.DiscussionId != null)
        {
            var discussion = await _context.Discussions.FirstOrDefaultAsync(d => d.PublicId == follow.DiscussionId.Value);
            if (discussion != null)
                entity = await _databaseRepository.GetByUserAndDiscussionAsync(user.Id, discussion.Id);
        }
        else if (follow.SpaceId != null)
        {
            var space = await _context.Spaces.FirstOrDefaultAsync(s => s.PublicId == follow.SpaceId.Value);
            if (space != null)
                entity = await _databaseRepository.GetByUserAndSpaceAsync(user.Id, space.Id);
        }
        else if (follow.FollowedUserId != null)
        {
            var followedUser = await _context.Users.FirstOrDefaultAsync(u => u.PublicId == follow.FollowedUserId.Value);
            if (followedUser != null)
                entity = await _databaseRepository.GetByUserAndFollowedUserAsync(user.Id, followedUser.Id);
        }

        if (entity == null) return;

        entity.Level = follow.Level.ToString();
        await _databaseRepository.UpdateAsync(entity);
        await _databaseRepository.SaveChangesAsync();
    }

    public async Task DeleteAsync(Follow follow)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.PublicId == follow.UserId.Value);
        if (user == null) return;

        Database.Entities.FollowDatabaseEntity? entity = null;

        if (follow.DiscussionId != null)
        {
            var discussion = await _context.Discussions.FirstOrDefaultAsync(d => d.PublicId == follow.DiscussionId.Value);
            if (discussion != null)
                entity = await _databaseRepository.GetByUserAndDiscussionAsync(user.Id, discussion.Id);
        }
        else if (follow.SpaceId != null)
        {
            var space = await _context.Spaces.FirstOrDefaultAsync(s => s.PublicId == follow.SpaceId.Value);
            if (space != null)
                entity = await _databaseRepository.GetByUserAndSpaceAsync(user.Id, space.Id);
        }
        else if (follow.FollowedUserId != null)
        {
            var followedUser = await _context.Users.FirstOrDefaultAsync(u => u.PublicId == follow.FollowedUserId.Value);
            if (followedUser != null)
                entity = await _databaseRepository.GetByUserAndFollowedUserAsync(user.Id, followedUser.Id);
        }

        if (entity == null) return;

        await _databaseRepository.DeleteAsync(entity);
        await _databaseRepository.SaveChangesAsync();
    }
}
