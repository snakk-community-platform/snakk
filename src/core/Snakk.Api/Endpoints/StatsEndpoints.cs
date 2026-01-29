namespace Snakk.Api.Endpoints;

using Microsoft.EntityFrameworkCore;
using Snakk.Infrastructure.Database;

public static class StatsEndpoints
{
    public static void MapStatsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api")
            .WithTags("Stats");

        group.MapGet("/platform/stats", GetPlatformStatsAsync)
            .WithName("GetPlatformStats");

        group.MapGet("/hubs/{publicId}/stats", GetHubStatsAsync)
            .WithName("GetHubStats");

        group.MapGet("/spaces/{publicId}/stats", GetSpaceStatsAsync)
            .WithName("GetSpaceStats");

        group.MapGet("/communities/{publicId}/stats", GetCommunityStatsAsync)
            .WithName("GetCommunityStats");

        group.MapGet("/users/{publicId}/stats", GetUserStatsAsync)
            .WithName("GetUserStats");

        group.MapGet("/discussions/{publicId}/stats", GetDiscussionStatsAsync)
            .WithName("GetDiscussionStats");
    }

    private static async Task<IResult> GetPlatformStatsAsync(SnakkDbContext dbContext)
    {
        // DbContext is not thread-safe, so run counts sequentially
        // These are simple COUNT queries so they're fast
        var hubCount = await dbContext.Hubs.AsNoTracking().CountAsync();
        var spaceCount = await dbContext.Spaces.AsNoTracking().CountAsync();
        var discussionCount = await dbContext.Discussions.AsNoTracking().CountAsync();
        var replyCount = await dbContext.Posts.AsNoTracking().CountAsync(p => !p.IsFirstPost);

        return Results.Ok(new
        {
            hubCount,
            spaceCount,
            discussionCount,
            replyCount
        });
    }

    private static async Task<IResult> GetHubStatsAsync(string publicId, SnakkDbContext dbContext)
    {
        // Single query using navigation properties
        var stats = await dbContext.Hubs.AsNoTracking()
            .Where(h => h.PublicId == publicId)
            .Select(h => new
            {
                publicId = h.PublicId,
                name = h.Name,
                description = h.Description,
                spaceCount = h.Spaces.Count(),
                discussionCount = h.Spaces.SelectMany(s => s.Discussions).Count(),
                replyCount = h.Spaces
                    .SelectMany(s => s.Discussions)
                    .SelectMany(d => d.Posts)
                    .Count(p => !p.IsFirstPost)
            })
            .FirstOrDefaultAsync();

        return stats != null ? Results.Ok(stats) : Results.NotFound();
    }

    private static async Task<IResult> GetSpaceStatsAsync(string publicId, SnakkDbContext dbContext)
    {
        // Single query using navigation properties
        var stats = await dbContext.Spaces.AsNoTracking()
            .Where(s => s.PublicId == publicId)
            .Select(s => new
            {
                publicId = s.PublicId,
                name = s.Name,
                description = s.Description,
                discussionCount = s.Discussions.Count(),
                replyCount = s.Discussions.SelectMany(d => d.Posts).Count(p => !p.IsFirstPost),
                followerCount = dbContext.Follows.Count(f => f.SpaceId == s.Id && f.TargetType == "Space")
            })
            .FirstOrDefaultAsync();

        return stats != null ? Results.Ok(stats) : Results.NotFound();
    }

    private static async Task<IResult> GetCommunityStatsAsync(string publicId, SnakkDbContext dbContext)
    {
        // Single query using navigation properties
        var stats = await dbContext.Communities.AsNoTracking()
            .Where(c => c.PublicId == publicId)
            .Select(c => new
            {
                publicId = c.PublicId,
                name = c.Name,
                description = c.Description,
                hubCount = c.Hubs.Count(),
                spaceCount = c.Hubs.SelectMany(h => h.Spaces).Count(),
                discussionCount = c.Hubs
                    .SelectMany(h => h.Spaces)
                    .SelectMany(s => s.Discussions)
                    .Count(),
                replyCount = c.Hubs
                    .SelectMany(h => h.Spaces)
                    .SelectMany(s => s.Discussions)
                    .SelectMany(d => d.Posts)
                    .Count(p => !p.IsFirstPost)
            })
            .FirstOrDefaultAsync();

        return stats != null ? Results.Ok(stats) : Results.NotFound();
    }

    private static async Task<IResult> GetUserStatsAsync(string publicId, SnakkDbContext dbContext)
    {
        var user = await dbContext.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.PublicId == publicId);

        if (user == null)
            return Results.NotFound();

        var discussionCount = await dbContext.Discussions.AsNoTracking()
            .CountAsync(d => d.CreatedByUserId == user.Id);

        var replyCount = await dbContext.Posts.AsNoTracking()
            .CountAsync(p => p.CreatedByUserId == user.Id && !p.IsFirstPost);

        var followerCount = await dbContext.Follows.AsNoTracking()
            .CountAsync(f => f.FollowedUserId == user.Id && f.TargetType == "User");

        return Results.Ok(new
        {
            publicId = user.PublicId,
            displayName = user.DisplayName,
            discussionCount,
            replyCount,
            followerCount
        });
    }

    private static async Task<IResult> GetDiscussionStatsAsync(string publicId, SnakkDbContext dbContext)
    {
        var discussion = await dbContext.Discussions.AsNoTracking()
            .FirstOrDefaultAsync(d => d.PublicId == publicId);

        if (discussion == null)
            return Results.NotFound();

        var replyCount = await dbContext.Posts.AsNoTracking()
            .CountAsync(p => p.DiscussionId == discussion.Id && !p.IsFirstPost);

        var followerCount = await dbContext.Follows.AsNoTracking()
            .CountAsync(f => f.DiscussionId == discussion.Id && f.TargetType == "Discussion");

        return Results.Ok(new
        {
            publicId = discussion.PublicId,
            title = discussion.Title,
            replyCount,
            followerCount
        });
    }
}
