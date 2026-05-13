namespace Snakk.Infrastructure.Database.Repositories;

using Microsoft.EntityFrameworkCore;
using Snakk.Application.Repositories;
using Snakk.Infrastructure.Database;
using Snakk.Shared.Enums;

public class ActivitySnapshotRepository(SnakkDbContext context) : IActivitySnapshotRepository
{
    // ── Read ──────────────────────────────────────────────────────────────────

    public async Task<List<ActivitySparklineDayDto>> GetSparklineAsync(
        ActivityEntityTypeEnum entityType,
        string? publicId,
        int days,
        CancellationToken ct = default)
    {
        var entityId = await ResolveEntityIdAsync(entityType, publicId, ct);
        if (entityId is null && entityType != ActivityEntityTypeEnum.Platform)
            return [];

        var since = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-(days - 1)));
        var id = entityId ?? 0;

        return await context.ActivityDailySnapshots
            .Where(s => s.EntityType == entityType && s.EntityId == id && s.Date >= since)
            .OrderBy(s => s.Date)
            .Select(s => new ActivitySparklineDayDto(s.Date, s.PostCount, s.DiscussionCount))
            .ToListAsync(ct);
    }

    public async Task<Dictionary<string, List<ActivitySparklineDayDto>>> GetSparklinesForSpacesAsync(
        IEnumerable<string> publicIds, int days, CancellationToken ct = default)
    {
        var ids = publicIds.Distinct().ToList();
        if (ids.Count == 0) return [];

        var since = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-(days - 1)));

        var spaceMap = await context.Spaces
            .Where(s => ids.Contains(s.PublicId))
            .Select(s => new { s.Id, s.PublicId })
            .ToDictionaryAsync(s => s.Id, s => s.PublicId, ct);

        if (spaceMap.Count == 0) return ids.ToDictionary(id => id, _ => new List<ActivitySparklineDayDto>());

        var entityIds = spaceMap.Keys.ToList();
        var entityType = ActivityEntityTypeEnum.Space;

        var rows = await context.ActivityDailySnapshots
            .Where(s => s.EntityType == entityType && entityIds.Contains(s.EntityId) && s.Date >= since)
            .Select(s => new { s.EntityId, s.Date, s.PostCount, s.DiscussionCount })
            .ToListAsync(ct);

        var result = ids.ToDictionary(id => id, _ => new List<ActivitySparklineDayDto>());
        foreach (var row in rows)
        {
            if (spaceMap.TryGetValue(row.EntityId, out var publicId) && result.TryGetValue(publicId, out var list))
                list.Add(new ActivitySparklineDayDto(row.Date, row.PostCount, row.DiscussionCount));
        }

        foreach (var list in result.Values)
            list.Sort((a, b) => a.Date.CompareTo(b.Date));

        return result;
    }

    // ── Refresh (called by worker) ────────────────────────────────────────────

    public async Task RefreshSnapshotsAsync(CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var yesterday = today.AddDays(-1);
        var cutoffUtc = yesterday.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        // ── Space-level post counts ───────────────────────────────────────────
        var spacePosts = await context.Posts
            .Where(p => !p.IsDeleted && p.CreatedAt >= cutoffUtc)
            .GroupBy(p => new { DateVal = p.CreatedAt.Date, p.SpaceId })
            .Select(g => new { g.Key.DateVal, g.Key.SpaceId, Count = g.Count() })
            .ToListAsync(ct);

        // ── Space-level discussion counts ─────────────────────────────────────
        var spaceDiscussions = await context.Discussions
            .Where(d => !d.IsDeleted && d.CreatedAt >= cutoffUtc)
            .GroupBy(d => new { DateVal = d.CreatedAt.Date, d.SpaceId })
            .Select(g => new { g.Key.DateVal, g.Key.SpaceId, Count = g.Count() })
            .ToListAsync(ct);

        // ── Discussion-level post counts ───────────────────────────────────────
        var discussionPosts = await context.Posts
            .Where(p => !p.IsDeleted && p.CreatedAt >= cutoffUtc)
            .GroupBy(p => new { DateVal = p.CreatedAt.Date, p.DiscussionId })
            .Select(g => new { g.Key.DateVal, g.Key.DiscussionId, Count = g.Count() })
            .ToListAsync(ct);

        // ── User-level post and discussion counts ──────────────────────────────
        var userPosts = await context.Posts
            .Where(p => !p.IsDeleted && p.CreatedAt >= cutoffUtc)
            .GroupBy(p => new { DateVal = p.CreatedAt.Date, p.CreatedByUserId })
            .Select(g => new { g.Key.DateVal, g.Key.CreatedByUserId, Count = g.Count() })
            .ToListAsync(ct);

        var userDiscussions = await context.Discussions
            .Where(d => !d.IsDeleted && d.CreatedAt >= cutoffUtc)
            .GroupBy(d => new { DateVal = d.CreatedAt.Date, d.CreatedByUserId })
            .Select(g => new { g.Key.DateVal, g.Key.CreatedByUserId, Count = g.Count() })
            .ToListAsync(ct);

        // ── Resolve hierarchy for affected spaces ──────────────────────────────
        var affectedSpaceIds = spacePosts.Select(x => x.SpaceId)
            .Concat(spaceDiscussions.Select(x => x.SpaceId))
            .Distinct().ToList();

        var spaces = await context.Spaces
            .Where(s => affectedSpaceIds.Contains(s.Id))
            .Select(s => new { s.Id, s.HubId })
            .ToListAsync(ct);

        var hubIds = spaces.Select(s => s.HubId).Distinct().ToList();
        var hubs = await context.Hubs
            .Where(h => hubIds.Contains(h.Id))
            .Select(h => new { h.Id, h.CommunityId })
            .ToListAsync(ct);

        var spaceToHub = spaces.ToDictionary(s => s.Id, s => s.HubId);
        var hubToCommunity = hubs.ToDictionary(h => h.Id, h => h.CommunityId);

        // ── Collect all rows, then batch-upsert once ──────────────────────────
        var rows = new List<(DateOnly Date, int EntityType, int EntityId, int PostCount, int DiscussionCount)>();

        foreach (var date in new[] { today, yesterday })
        {
            var dateTime = date.ToDateTime(TimeOnly.MinValue);

            var daySpacePosts        = spacePosts.Where(x => x.DateVal == dateTime).ToDictionary(x => x.SpaceId, x => x.Count);
            var daySpaceDiscussions  = spaceDiscussions.Where(x => x.DateVal == dateTime).ToDictionary(x => x.SpaceId, x => x.Count);
            var hubPosts             = new Dictionary<int, int>();
            var hubDiscussions       = new Dictionary<int, int>();
            var communityPosts       = new Dictionary<int, int>();
            var communityDiscussions = new Dictionary<int, int>();

            foreach (var spaceId in daySpacePosts.Keys.Concat(daySpaceDiscussions.Keys).Distinct())
            {
                var p = daySpacePosts.GetValueOrDefault(spaceId);
                var d = daySpaceDiscussions.GetValueOrDefault(spaceId);

                rows.Add((date, (int)ActivityEntityTypeEnum.Space, spaceId, p, d));

                if (!spaceToHub.TryGetValue(spaceId, out var hubId)) continue;
                hubPosts[hubId]        = hubPosts.GetValueOrDefault(hubId) + p;
                hubDiscussions[hubId]  = hubDiscussions.GetValueOrDefault(hubId) + d;

                if (!hubToCommunity.TryGetValue(hubId, out var communityId)) continue;
                communityPosts[communityId]        = communityPosts.GetValueOrDefault(communityId) + p;
                communityDiscussions[communityId]  = communityDiscussions.GetValueOrDefault(communityId) + d;
            }

            foreach (var (hubId, p) in hubPosts)
                rows.Add((date, (int)ActivityEntityTypeEnum.Hub, hubId, p, hubDiscussions.GetValueOrDefault(hubId)));

            foreach (var (communityId, p) in communityPosts)
                rows.Add((date, (int)ActivityEntityTypeEnum.Community, communityId, p, communityDiscussions.GetValueOrDefault(communityId)));

            var totalPosts       = communityPosts.Values.Sum();
            var totalDiscussions = communityDiscussions.Values.Sum();
            if (totalPosts > 0 || totalDiscussions > 0)
                rows.Add((date, (int)ActivityEntityTypeEnum.Platform, 0, totalPosts, totalDiscussions));

            foreach (var x in discussionPosts.Where(x => x.DateVal == dateTime))
                rows.Add((date, (int)ActivityEntityTypeEnum.Discussion, x.DiscussionId, x.Count, 0));

            foreach (var x in userPosts.Where(x => x.DateVal == dateTime))
            {
                var d = userDiscussions.FirstOrDefault(u => u.DateVal == dateTime && u.CreatedByUserId == x.CreatedByUserId)?.Count ?? 0;
                rows.Add((date, (int)ActivityEntityTypeEnum.User, x.CreatedByUserId, x.Count, d));
            }
            foreach (var x in userDiscussions.Where(x => x.DateVal == dateTime && !userPosts.Any(p => p.DateVal == dateTime && p.CreatedByUserId == x.CreatedByUserId)))
                rows.Add((date, (int)ActivityEntityTypeEnum.User, x.CreatedByUserId, 0, x.Count));
        }

        if (rows.Count == 0) return;

        var dates      = rows.Select(r => r.Date).ToArray();
        var types      = rows.Select(r => r.EntityType).ToArray();
        var ids        = rows.Select(r => r.EntityId).ToArray();
        var posts      = rows.Select(r => r.PostCount).ToArray();
        var discs      = rows.Select(r => r.DiscussionCount).ToArray();

        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "ActivityDailySnapshot" ("Date", "EntityType", "EntityId", "PostCount", "DiscussionCount")
            SELECT UNNEST({dates}), UNNEST({types}), UNNEST({ids}), UNNEST({posts}), UNNEST({discs})
            ON CONFLICT ("Date", "EntityType", "EntityId")
            DO UPDATE SET
                "PostCount"       = EXCLUDED."PostCount",
                "DiscussionCount" = EXCLUDED."DiscussionCount"
            """, ct);
    }

    // ── Prune ─────────────────────────────────────────────────────────────────

    public async Task PruneAsync(int retainDays, CancellationToken ct = default)
    {
        var cutoff = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-retainDays));
        await context.ActivityDailySnapshots
            .Where(s => s.Date < cutoff)
            .ExecuteDeleteAsync(ct);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<int?> ResolveEntityIdAsync(ActivityEntityTypeEnum entityType, string? publicId, CancellationToken ct)
    {
        if (entityType == ActivityEntityTypeEnum.Platform)
            return 0;

        if (string.IsNullOrEmpty(publicId))
            return null;

        return entityType switch
        {
            ActivityEntityTypeEnum.Space      => await context.Spaces.Where(s => s.PublicId == publicId).Select(s => (int?)s.Id).FirstOrDefaultAsync(ct),
            ActivityEntityTypeEnum.Hub        => await context.Hubs.Where(h => h.PublicId == publicId).Select(h => (int?)h.Id).FirstOrDefaultAsync(ct),
            ActivityEntityTypeEnum.Community  => await context.Communities.Where(c => c.PublicId == publicId).Select(c => (int?)c.Id).FirstOrDefaultAsync(ct),
            ActivityEntityTypeEnum.Discussion => await context.Discussions.Where(d => d.PublicId == publicId).Select(d => (int?)d.Id).FirstOrDefaultAsync(ct),
            ActivityEntityTypeEnum.User       => await context.Users.Where(u => u.PublicId == publicId).Select(u => (int?)u.Id).FirstOrDefaultAsync(ct),
            _ => null
        };
    }
}
