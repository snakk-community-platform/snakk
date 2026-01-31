using Microsoft.EntityFrameworkCore;
using Snakk.Application.Repositories;
using Snakk.Infrastructure.Database;
using Snakk.Shared.Enums;

namespace Snakk.Infrastructure.Database.Repositories;

public class StatsRepository : IStatsRepository
{
    private readonly SnakkDbContext _context;

    public StatsRepository(SnakkDbContext context)
    {
        _context = context;
    }

    public async Task<PlatformStatsDto> GetPlatformStatsAsync()
    {
        // Run counts sequentially since DbContext is not thread-safe
        var hubCount = await _context.Hubs.AsNoTracking().CountAsync();
        var spaceCount = await _context.Spaces.AsNoTracking().CountAsync();
        var discussionCount = await _context.Discussions.AsNoTracking().CountAsync();
        var replyCount = await _context.Posts.AsNoTracking().CountAsync(p => !p.IsFirstPost);

        return new PlatformStatsDto(hubCount, spaceCount, discussionCount, replyCount);
    }

    public async Task<HubStatsDto?> GetHubStatsAsync(string publicId)
    {
        var stats = await _context.Hubs.AsNoTracking()
            .Where(h => h.PublicId == publicId)
            .Select(h => new HubStatsDto(
                h.PublicId,
                h.Name,
                h.Description,
                h.Spaces.Count(),
                h.Spaces.SelectMany(s => s.Discussions).Count(),
                h.Spaces
                    .SelectMany(s => s.Discussions)
                    .SelectMany(d => d.Posts)
                    .Count(p => !p.IsFirstPost)
            ))
            .FirstOrDefaultAsync();

        return stats;
    }

    public async Task<SpaceStatsDto?> GetSpaceStatsAsync(string publicId)
    {
        var stats = await _context.Spaces.AsNoTracking()
            .Where(s => s.PublicId == publicId)
            .Select(s => new SpaceStatsDto(
                s.PublicId,
                s.Name,
                s.Description,
                s.Discussions.Count(),
                s.Discussions.SelectMany(d => d.Posts).Count(p => !p.IsFirstPost),
                _context.Follows.Count(f => f.SpaceId == s.Id && f.TargetTypeId == (int)FollowTargetTypeEnum.Space)
            ))
            .FirstOrDefaultAsync();

        return stats;
    }

    public async Task<CommunityStatsDto?> GetCommunityStatsAsync(string publicId)
    {
        var stats = await _context.Communities.AsNoTracking()
            .Where(c => c.PublicId == publicId)
            .Select(c => new CommunityStatsDto(
                c.PublicId,
                c.Name,
                c.Description,
                c.Hubs.Count(),
                c.Hubs.SelectMany(h => h.Spaces).Count(),
                c.Hubs
                    .SelectMany(h => h.Spaces)
                    .SelectMany(s => s.Discussions)
                    .Count(),
                c.Hubs
                    .SelectMany(h => h.Spaces)
                    .SelectMany(s => s.Discussions)
                    .SelectMany(d => d.Posts)
                    .Count(p => !p.IsFirstPost)
            ))
            .FirstOrDefaultAsync();

        return stats;
    }

    public async Task<UserStatsDto?> GetUserStatsAsync(string publicId)
    {
        var user = await _context.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.PublicId == publicId);

        if (user == null)
            return null;

        var discussionCount = await _context.Discussions.AsNoTracking()
            .CountAsync(d => d.CreatedByUserId == user.Id);

        var replyCount = await _context.Posts.AsNoTracking()
            .CountAsync(p => p.CreatedByUserId == user.Id && !p.IsFirstPost);

        var followerCount = await _context.Follows.AsNoTracking()
            .CountAsync(f => f.FollowedUserId == user.Id && f.TargetTypeId == (int)FollowTargetTypeEnum.User);

        return new UserStatsDto(
            user.PublicId,
            user.DisplayName,
            discussionCount,
            replyCount,
            followerCount);
    }

    public async Task<DiscussionStatsDto?> GetDiscussionStatsAsync(string publicId)
    {
        var discussion = await _context.Discussions.AsNoTracking()
            .FirstOrDefaultAsync(d => d.PublicId == publicId);

        if (discussion == null)
            return null;

        var replyCount = await _context.Posts.AsNoTracking()
            .CountAsync(p => p.DiscussionId == discussion.Id && !p.IsFirstPost);

        var followerCount = await _context.Follows.AsNoTracking()
            .CountAsync(f => f.DiscussionId == discussion.Id && f.TargetTypeId == (int)FollowTargetTypeEnum.Discussion);

        return new DiscussionStatsDto(
            discussion.PublicId,
            discussion.Title,
            replyCount,
            followerCount);
    }

    public async Task<List<TopActiveSpaceDto>> GetTopActiveSpacesTodayAsync(
        string? hubId = null,
        string? communityId = null,
        int limit = 5)
    {
        var today = DateTime.UtcNow.Date;

        var postsQuery = _context.Posts
            .AsNoTracking()
            .Where(p => !p.IsDeleted && p.CreatedAt >= today);

        // Filter by community if specified
        if (!string.IsNullOrEmpty(communityId))
        {
            postsQuery = postsQuery
                .Where(p => p.Discussion.Space.Hub.Community.PublicId == communityId);
        }

        // Filter by hub if specified
        if (!string.IsNullOrEmpty(hubId))
        {
            postsQuery = postsQuery
                .Where(p => p.Discussion.Space.Hub.PublicId == hubId);
        }

        var topSpaces = await postsQuery
            .GroupBy(p => p.Discussion.SpaceId)
            .Select(g => new { SpaceId = g.Key, PostCountToday = g.Count() })
            .OrderByDescending(x => x.PostCountToday)
            .Take(limit)
            .Join(
                _context.Spaces.AsNoTracking().Where(s => !s.IsDeleted),
                x => x.SpaceId,
                s => s.Id,
                (x, s) => new TopActiveSpaceDto(
                    s.PublicId,
                    s.Name,
                    s.Slug,
                    x.PostCountToday,
                    s.Hub.PublicId,
                    s.Hub.Slug,
                    s.Hub.Name
                ))
            .ToListAsync();

        return topSpaces;
    }
}
