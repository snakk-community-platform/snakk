namespace Snakk.Infrastructure.Database.Repositories;

using Microsoft.EntityFrameworkCore;
using Snakk.Application.Repositories;
using Snakk.Shared.Enums;

public class DashboardChartRepository(SnakkDbContext context, IDbContextFactory<SnakkDbContext> dbFactory) : IDashboardChartRepository
{
    private readonly SnakkDbContext _context = context;

    private async Task<T> ReadAsync<T>(Func<SnakkDbContext, Task<T>> query, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        return await query(db);
    }

    public async Task<List<DailyActivityData>> GetDailyActivityAsync(
        string scopeType, string scopePublicId, int days, CancellationToken ct = default)
    {
        var since = DateTime.UtcNow.Date.AddDays(-days);

        var discussTask = ReadAsync(db =>
        {
            var q = scopeType switch
            {
                "Community" => db.Discussions.Where(d => d.CommunityPublicId == scopePublicId),
                "Hub"       => db.Discussions.Where(d => d.HubPublicId == scopePublicId),
                "Space"     => db.Discussions.Where(d => d.SpacePublicId == scopePublicId),
                "Site"      => db.Discussions.AsQueryable(),
                _ => throw new ArgumentException($"Unknown scope type: {scopeType}")
            };
            return q.Where(d => !d.IsDeleted && d.CreatedAt >= since)
                .GroupBy(d => d.CreatedAt.Date)
                .Select(g => new { Date = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Date, x => x.Count);
        }, ct);

        var postTask = ReadAsync(db =>
        {
            var q = scopeType switch
            {
                "Community" => db.Posts.Where(p => p.CommunityPublicId == scopePublicId),
                "Hub"       => db.Posts.Where(p => p.HubPublicId == scopePublicId),
                "Space"     => db.Posts.Where(p => p.SpacePublicId == scopePublicId),
                "Site"      => db.Posts.AsQueryable(),
                _ => throw new ArgumentException($"Unknown scope type: {scopeType}")
            };
            return q.Where(p => !p.IsDeleted && p.CreatedAt >= since)
                .GroupBy(p => p.CreatedAt.Date)
                .Select(g => new { Date = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Date, x => x.Count);
        }, ct);

        await Task.WhenAll(discussTask, postTask);
        var discussionsByDay = discussTask.Result;
        var postsByDay       = postTask.Result;

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
        string scopeType, string scopePublicId, int weeks, CancellationToken ct = default)
    {
        var since = StartOfWeek(DateTime.UtcNow).AddDays(-7d * weeks);

        var openedTask = ReadAsync(db =>
        {
            var q = scopeType switch
            {
                "Community" => db.Reports.Where(r => r.Community != null && r.Community.PublicId == scopePublicId),
                "Hub"       => db.Reports.Where(r => (r.Hub != null && r.Hub.PublicId == scopePublicId) || (r.Space != null && r.Space.HubPublicId == scopePublicId)),
                "Space"     => db.Reports.Where(r => r.Space != null && r.Space.PublicId == scopePublicId),
                "Site"      => db.Reports.AsQueryable(),
                _ => throw new ArgumentException($"Unknown scope type: {scopeType}")
            };
            return q.Where(r => r.CreatedAt >= since)
                .GroupBy(r => r.CreatedAt.Date)
                .Select(g => new { Date = g.Key, Count = g.Count() })
                .ToListAsync();
        }, ct);

        var resolvedTask = ReadAsync(db =>
        {
            var q = scopeType switch
            {
                "Community" => db.Reports.Where(r => r.Community != null && r.Community.PublicId == scopePublicId),
                "Hub"       => db.Reports.Where(r => (r.Hub != null && r.Hub.PublicId == scopePublicId) || (r.Space != null && r.Space.HubPublicId == scopePublicId)),
                "Space"     => db.Reports.Where(r => r.Space != null && r.Space.PublicId == scopePublicId),
                "Site"      => db.Reports.AsQueryable(),
                _ => throw new ArgumentException($"Unknown scope type: {scopeType}")
            };
            return q.Where(r => r.ResolvedAt != null && r.ResolvedAt >= since)
                .GroupBy(r => r.ResolvedAt!.Value.Date)
                .Select(g => new { Date = g.Key, Count = g.Count() })
                .ToListAsync();
        }, ct);

        await Task.WhenAll(openedTask, resolvedTask);
        var openedByWeek   = openedTask.Result;
        var resolvedByWeek = resolvedTask.Result;

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
        string scopeType, string scopePublicId, int days, CancellationToken ct = default)
    {
        var since = DateTime.UtcNow.Date.AddDays(-days);

        var query = scopeType switch
        {
            "Community" => _context.PostReactions
                .Where(r => r.Post.CommunityPublicId == scopePublicId),
            "Hub" => _context.PostReactions
                .Where(r => r.Post.HubPublicId == scopePublicId),
            "Space" => _context.PostReactions
                .Where(r => r.Post.SpacePublicId == scopePublicId),
            "Site" => _context.PostReactions.AsQueryable(),
            _ => throw new ArgumentException($"Unknown scope type: {scopeType}")
        };

        var grouped = await query
            .Where(r => r.CreatedAt >= since)
            .GroupBy(r => r.TypeId)
            .Select(g => new { TypeId = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return grouped
            .Select(g => new ReactionBreakdownData(
                ((ReactionTypeEnum)g.TypeId).ToString(),
                g.Count))
            .OrderByDescending(r => r.Count)
            .ToList();
    }

    public async Task<List<ContentTypeBreakdownData>> GetContentTypeBreakdownAsync(
        string scopeType, string scopePublicId, int days, CancellationToken ct = default)
    {
        var since = DateTime.UtcNow.Date.AddDays(-days);

        var query = scopeType switch
        {
            "Community" => _context.Discussions
                .Where(d => d.CommunityPublicId == scopePublicId),
            "Hub" => _context.Discussions
                .Where(d => d.HubPublicId == scopePublicId),
            "Space" => _context.Discussions
                .Where(d => d.SpacePublicId == scopePublicId),
            "Site" => _context.Discussions.AsQueryable(),
            _ => throw new ArgumentException($"Unknown scope type: {scopeType}")
        };

        var grouped = await query
            .Where(d => !d.IsDeleted && d.CreatedAt >= since)
            .GroupBy(d => d.Type)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        return grouped
            .Select(g => new ContentTypeBreakdownData(
                ((DiscussionTypeEnum)g.Type).ToString(),
                g.Count))
            .OrderByDescending(c => c.Count)
            .ToList();
    }

    public async Task<List<DailyEngagementData>> GetDailyEngagementAsync(
        string scopeType, string scopePublicId, int days, CancellationToken ct = default)
    {
        var since = DateTime.UtcNow.Date.AddDays(-days);

        var query = scopeType switch
        {
            "Community" => _context.PostReactions
                .Where(r => r.Post.CommunityPublicId == scopePublicId),
            "Hub" => _context.PostReactions
                .Where(r => r.Post.HubPublicId == scopePublicId),
            "Space" => _context.PostReactions
                .Where(r => r.Post.SpacePublicId == scopePublicId),
            "Site" => _context.PostReactions.AsQueryable(),
            _ => throw new ArgumentException($"Unknown scope type: {scopeType}")
        };

        var reactionsByDay = await query
            .Where(r => r.CreatedAt >= since)
            .GroupBy(r => r.CreatedAt.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Date, x => x.Count, ct);

        var result = new List<DailyEngagementData>();

        for (var date = since; date <= DateTime.UtcNow.Date; date = date.AddDays(1))
        {
            reactionsByDay.TryGetValue(date, out var reactions);
            result.Add(new DailyEngagementData(date, reactions));
        }

        return result;
    }

    public async Task<List<TopDiscussionData>> GetTopDiscussionsAsync(
        string scopeType, string scopePublicId, int days, int count = 10, CancellationToken ct = default)
    {
        var since = DateTime.UtcNow.Date.AddDays(-days);

        var query = scopeType switch
        {
            "Community" => _context.Discussions
                .Where(d => d.CommunityPublicId == scopePublicId),
            "Hub" => _context.Discussions
                .Where(d => d.HubPublicId == scopePublicId),
            "Space" => _context.Discussions
                .Where(d => d.SpacePublicId == scopePublicId),
            "Site" => _context.Discussions.AsQueryable(),
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
            .ToListAsync(ct);
    }

    public async Task<List<TopContributorData>> GetTopContributorsForScopeAsync(
        string scopeType, string scopePublicId, int days, int limit = 10, CancellationToken ct = default)
    {
        var since = DateTime.UtcNow.Date.AddDays(-days);

        var q = scopeType switch
        {
            "Community" => _context.Posts.Where(p => p.CommunityPublicId == scopePublicId),
            "Hub"       => _context.Posts.Where(p => p.HubPublicId == scopePublicId),
            "Space"     => _context.Posts.Where(p => p.SpacePublicId == scopePublicId),
            "Site"      => _context.Posts.AsQueryable(),
            _ => throw new ArgumentException($"Unknown scope type: {scopeType}")
        };

        var counts = await q
            .Where(p => !p.IsDeleted && p.CreatedAt >= since)
            .GroupBy(p => new { p.CreatedByUserId, p.CreatedByUserPublicId })
            .Select(g => new
            {
                UserId = g.Key.CreatedByUserId,
                UserPublicId = g.Key.CreatedByUserPublicId,
                PostCount = g.Count(p => !p.IsFirstPost),
                DiscussionCount = g.Count(p => p.IsFirstPost)
            })
            .OrderByDescending(x => x.PostCount + x.DiscussionCount)
            .Take(limit)
            .ToListAsync(ct);

        var userIds = counts.Select(x => x.UserId).ToList();
        var users = await _context.Users
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName, u.AvatarThumbnailFileName })
            .ToDictionaryAsync(u => u.Id, ct);

        return counts.Select(x =>
        {
            users.TryGetValue(x.UserId, out var u);
            return new TopContributorData(
                x.UserPublicId ?? "",
                u?.DisplayName ?? "",
                u?.AvatarThumbnailFileName,
                x.PostCount,
                x.DiscussionCount);
        }).ToList();
    }

    public async Task<List<DailyRegistrationData>> GetDailyRegistrationsAsync(
        int days, CancellationToken ct = default)
    {
        var since = DateTime.UtcNow.Date.AddDays(-days);

        var regsByDay = await _context.Users
            .Where(u => !u.IsDeleted && u.CreatedAt >= since)
            .GroupBy(u => u.CreatedAt.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Date, x => x.Count, ct);

        var result = new List<DailyRegistrationData>();
        for (var date = since; date <= DateTime.UtcNow.Date; date = date.AddDays(1))
        {
            regsByDay.TryGetValue(date, out var count);
            result.Add(new DailyRegistrationData(date, count));
        }
        return result;
    }

    public async Task<(int Dau, int Wau, int Mau, int Total)> GetUserActivityCountsAsync(
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var dauSince = now.AddDays(-1);
        var wauSince = now.AddDays(-7);
        var mauSince = now.AddDays(-30);

        var dauTask = _context.Users.CountAsync(u => !u.IsDeleted && u.LastSeenAt >= dauSince, ct);
        var wauTask = _context.Users.CountAsync(u => !u.IsDeleted && u.LastSeenAt >= wauSince, ct);
        var mauTask = _context.Users.CountAsync(u => !u.IsDeleted && u.LastSeenAt >= mauSince, ct);
        var totalTask = _context.Users.CountAsync(u => !u.IsDeleted, ct);

        await Task.WhenAll(dauTask, wauTask, mauTask, totalTask);
        return (dauTask.Result, wauTask.Result, mauTask.Result, totalTask.Result);
    }

    private static DateTime StartOfWeek(DateTime date)
    {
        var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
        return date.Date.AddDays(-diff);
    }
}
