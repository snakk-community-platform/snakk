using Microsoft.EntityFrameworkCore;
using Snakk.Application.Repositories;
using Snakk.Infrastructure.Database;
using Snakk.Shared.Enums;

namespace Snakk.Infrastructure.Database.Repositories;

public class StatsRepository(SnakkDbContext context) : IStatsRepository
{
    private readonly SnakkDbContext _context = context;

    public async Task<PlatformStatsDto> GetPlatformStatsAsync()
    {
        // Use denormalized counters from Community entities (avoids full table scans on Posts)
        var stats = await _context.Communities
            .GroupBy(_ => 1)
            .Select(g => new {
                HubCount = g.Sum(c => c.HubCount),
                SpaceCount = g.Sum(c => c.SpaceCount),
                DiscussionCount = g.Sum(c => c.DiscussionCount),
                PostCount = g.Sum(c => c.PostCount) })
            .FirstOrDefaultAsync();

        if (stats is null)
            return new PlatformStatsDto(0, 0, 0, 0);

        // ReplyCount = total posts minus first posts (one per discussion)
        var replyCount = stats.PostCount - stats.DiscussionCount;

        return new PlatformStatsDto(stats.HubCount, stats.SpaceCount, stats.DiscussionCount, replyCount);
    }

    public async Task<HubStatsDto?> GetHubStatsAsync(string publicId) =>
        // Use denormalized counters (avoids SelectMany chains across tables)
        await _context.Hubs
            .Where(h => h.PublicId == publicId)
            .Select(h => new HubStatsDto(
                h.PublicId,
                h.Name,
                h.Description,
                h.SpaceCount,
                h.DiscussionCount,
                h.PostCount - h.DiscussionCount))
            .FirstOrDefaultAsync();

    public async Task<SpaceStatsDto?> GetSpaceStatsAsync(string publicId) =>
        // Use denormalized counters for discussion/reply counts
        await _context.Spaces
            .Where(s => s.PublicId == publicId)
            .Select(s => new SpaceStatsDto(
                s.PublicId,
                s.Name,
                s.Description,
                s.DiscussionCount,
                s.PostCount - s.DiscussionCount,
                _context.Follows.Count(f =>
                    f.SpaceId == s.Id
                    && f.TargetTypeId == (int)FollowTargetTypeEnum.Space)))
            .FirstOrDefaultAsync();

    public async Task<CommunityStatsDto?> GetCommunityStatsAsync(string publicId) =>
        // Use denormalized counters (avoids SelectMany chains across millions of posts)
        await _context.Communities
            .Where(c => c.PublicId == publicId)
            .Select(c => new CommunityStatsDto(
                c.PublicId,
                c.Name,
                c.Description,
                c.HubCount,
                c.SpaceCount,
                c.DiscussionCount,
                c.PostCount - c.DiscussionCount))
            .FirstOrDefaultAsync();

    public async Task<UserStatsDto?> GetUserStatsAsync(string publicId) => await _context.Users
        .Where(u => u.PublicId == publicId)
        .Select(u => new UserStatsDto(
            u.PublicId,
            u.DisplayName,
            u.DiscussionCount,
            u.ReplyCount,
            u.FollowerCount))
        .FirstOrDefaultAsync();

    public async Task<DiscussionStatsDto?> GetDiscussionStatsAsync(string publicId)
    {
        var discussion = await _context.Discussions
            .FirstOrDefaultAsync(d => d.PublicId == publicId);

        if (discussion is null)
            return null;

        return new DiscussionStatsDto(
            discussion.PublicId,
            discussion.Title,
            discussion.PostCount - 1, // PostCount includes first post; replies = total - 1
            discussion.FollowerCount);
    }

    public async Task<List<TopActiveSpaceDto>> GetTopActiveSpacesSinceAsync(
        DateTime since,
        string? hubId = null,
        string? communityId = null,
        int limit = 5)
    {
        var postsQuery = _context.Posts
            .Where(p => !p.IsDeleted && p.CreatedAt >= since);

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
            .Select(g => new {
                SpaceId = g.Key,
                PostCountToday = g.Count() })
            .OrderByDescending(x => x.PostCountToday)
            .Take(limit)
            .Join(
                _context.Spaces.Where(s => !s.IsDeleted),
                x => x.SpaceId,
                s => s.Id,
                (x, s) => new TopActiveSpaceDto(
                    s.PublicId,
                    s.Name,
                    s.Slug,
                    x.PostCountToday,
                    s.Hub.PublicId,
                    s.Hub.Slug,
                    s.Hub.Name,
                    s.Hub.Community.Slug))
            .ToListAsync();

        return topSpaces;
    }
}
