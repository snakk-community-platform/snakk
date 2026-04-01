namespace Snakk.Infrastructure.Database.Repositories;

using Microsoft.EntityFrameworkCore;
using Snakk.Application.Repositories;
using Snakk.Shared.Enums;

public class DashboardChartRepository(SnakkDbContext context) : IDashboardChartRepository
{
    private readonly SnakkDbContext _context = context;

    public async Task<List<DailyActivityData>> GetDailyActivityAsync(
        string scopeType, string scopePublicId, int days)
    {
        var since = DateTime.UtcNow.Date.AddDays(-days);

        var discussionQuery = scopeType switch
        {
            "Community" => _context.Discussions
                .Where(d => d.Space.Hub.Community.PublicId == scopePublicId),
            "Hub" => _context.Discussions
                .Where(d => d.Space.Hub.PublicId == scopePublicId),
            "Space" => _context.Discussions
                .Where(d => d.Space.PublicId == scopePublicId),
            _ => throw new ArgumentException($"Unknown scope type: {scopeType}")
        };

        var postQuery = scopeType switch
        {
            "Community" => _context.Posts
                .Where(p => p.Discussion.Space.Hub.Community.PublicId == scopePublicId),
            "Hub" => _context.Posts
                .Where(p => p.Discussion.Space.Hub.PublicId == scopePublicId),
            "Space" => _context.Posts
                .Where(p => p.Discussion.Space.PublicId == scopePublicId),
            _ => throw new ArgumentException($"Unknown scope type: {scopeType}")
        };

        var discussionsByDay = await discussionQuery
            .Where(d => !d.IsDeleted && d.CreatedAt >= since)
            .GroupBy(d => d.CreatedAt.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Date, x => x.Count);

        var postsByDay = await postQuery
            .Where(p => !p.IsDeleted && p.CreatedAt >= since)
            .GroupBy(p => p.CreatedAt.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Date, x => x.Count);

        var result = new List<DailyActivityData>();

        for (var date = since; date <= DateTime.UtcNow.Date; date = date.AddDays(1))
        {
            discussionsByDay.TryGetValue(date, out var discussions);
            postsByDay.TryGetValue(date, out var posts);
            result.Add(new DailyActivityData(date, discussions, posts));
        }

        return result;
    }

    public async Task<List<WeeklyModerationData>> GetWeeklyModerationAsync(
        string scopeType, string scopePublicId, int weeks)
    {
        var since = StartOfWeek(DateTime.UtcNow).AddDays(-7d * weeks);

        var reportQuery = scopeType switch
        {
            "Community" => _context.Reports
                .Where(r => r.Community != null && r.Community.PublicId == scopePublicId),
            "Hub" => _context.Reports
                .Where(r =>
                    (r.Hub != null && r.Hub.PublicId == scopePublicId)
                    || (r.Space != null && r.Space.Hub.PublicId == scopePublicId)),
            "Space" => _context.Reports
                .Where(r => r.Space != null && r.Space.PublicId == scopePublicId),
            _ => throw new ArgumentException($"Unknown scope type: {scopeType}")
        };

        var openedByWeek = await reportQuery
            .Where(r => r.CreatedAt >= since)
            .GroupBy(r => r.CreatedAt.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToListAsync();

        var resolvedByWeek = await reportQuery
            .Where(r => r.ResolvedAt != null && r.ResolvedAt >= since)
            .GroupBy(r => r.ResolvedAt!.Value.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToListAsync();

        var openedLookup = openedByWeek
            .GroupBy(x => StartOfWeek(x.Date))
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Count));

        var resolvedLookup = resolvedByWeek
            .GroupBy(x => StartOfWeek(x.Date))
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Count));

        var result = new List<WeeklyModerationData>();
        var currentWeekStart = StartOfWeek(DateTime.UtcNow);

        for (var weekStart = since; weekStart <= currentWeekStart; weekStart = weekStart.AddDays(7))
        {
            openedLookup.TryGetValue(weekStart, out var opened);
            resolvedLookup.TryGetValue(weekStart, out var resolved);
            result.Add(new WeeklyModerationData(weekStart, opened, resolved));
        }

        return result;
    }

    public async Task<List<ReactionBreakdownData>> GetReactionBreakdownAsync(
        string scopeType, string scopePublicId, int days)
    {
        var since = DateTime.UtcNow.Date.AddDays(-days);

        var query = scopeType switch
        {
            "Community" => _context.Reactions
                .Where(r => r.Post.Discussion.Space.Hub.Community.PublicId == scopePublicId),
            "Hub" => _context.Reactions
                .Where(r => r.Post.Discussion.Space.Hub.PublicId == scopePublicId),
            "Space" => _context.Reactions
                .Where(r => r.Post.Discussion.Space.PublicId == scopePublicId),
            _ => throw new ArgumentException($"Unknown scope type: {scopeType}")
        };

        var grouped = await query
            .Where(r => r.CreatedAt >= since)
            .GroupBy(r => r.TypeId)
            .Select(g => new { TypeId = g.Key, Count = g.Count() })
            .ToListAsync();

        return grouped
            .Select(g => new ReactionBreakdownData(
                ((ReactionTypeEnum)g.TypeId).ToString(),
                g.Count))
            .OrderByDescending(r => r.Count)
            .ToList();
    }

    public async Task<List<ContentTypeBreakdownData>> GetContentTypeBreakdownAsync(
        string scopeType, string scopePublicId, int days)
    {
        var since = DateTime.UtcNow.Date.AddDays(-days);

        var query = scopeType switch
        {
            "Community" => _context.Discussions
                .Where(d => d.Space.Hub.Community.PublicId == scopePublicId),
            "Hub" => _context.Discussions
                .Where(d => d.Space.Hub.PublicId == scopePublicId),
            "Space" => _context.Discussions
                .Where(d => d.Space.PublicId == scopePublicId),
            _ => throw new ArgumentException($"Unknown scope type: {scopeType}")
        };

        var grouped = await query
            .Where(d => !d.IsDeleted && d.CreatedAt >= since)
            .GroupBy(d => d.Type)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToListAsync();

        return grouped
            .Select(g => new ContentTypeBreakdownData(
                ((DiscussionTypeEnum)g.Type).ToString(),
                g.Count))
            .OrderByDescending(c => c.Count)
            .ToList();
    }

    public async Task<List<DailyEngagementData>> GetDailyEngagementAsync(
        string scopeType, string scopePublicId, int days)
    {
        var since = DateTime.UtcNow.Date.AddDays(-days);

        var query = scopeType switch
        {
            "Community" => _context.Reactions
                .Where(r => r.Post.Discussion.Space.Hub.Community.PublicId == scopePublicId),
            "Hub" => _context.Reactions
                .Where(r => r.Post.Discussion.Space.Hub.PublicId == scopePublicId),
            "Space" => _context.Reactions
                .Where(r => r.Post.Discussion.Space.PublicId == scopePublicId),
            _ => throw new ArgumentException($"Unknown scope type: {scopeType}")
        };

        var reactionsByDay = await query
            .Where(r => r.CreatedAt >= since)
            .GroupBy(r => r.CreatedAt.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Date, x => x.Count);

        var result = new List<DailyEngagementData>();

        for (var date = since; date <= DateTime.UtcNow.Date; date = date.AddDays(1))
        {
            reactionsByDay.TryGetValue(date, out var reactions);
            result.Add(new DailyEngagementData(date, reactions));
        }

        return result;
    }

    public async Task<List<TopDiscussionData>> GetTopDiscussionsAsync(
        string scopeType, string scopePublicId, int days, int count = 10)
    {
        var since = DateTime.UtcNow.Date.AddDays(-days);

        var query = scopeType switch
        {
            "Community" => _context.Discussions
                .Where(d => d.Space.Hub.Community.PublicId == scopePublicId),
            "Hub" => _context.Discussions
                .Where(d => d.Space.Hub.PublicId == scopePublicId),
            "Space" => _context.Discussions
                .Where(d => d.Space.PublicId == scopePublicId),
            _ => throw new ArgumentException($"Unknown scope type: {scopeType}")
        };

        return await query
            .Where(d => !d.IsDeleted && d.CreatedAt >= since)
            .OrderByDescending(d => d.PostCount)
            .ThenByDescending(d => d.ReactionCount)
            .Take(count)
            .Select(d => new TopDiscussionData(
                d.Title,
                d.Slug,
                d.Space.Slug,
                d.PostCount,
                d.ReactionCount))
            .ToListAsync();
    }

    private static DateTime StartOfWeek(DateTime date)
    {
        var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        return date.Date.AddDays(-diff);
    }
}
