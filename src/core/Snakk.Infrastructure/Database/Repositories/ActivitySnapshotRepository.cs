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

    // ── Refresh (called by worker) ────────────────────────────────────────────

    public async Task RefreshSnapshotsAsync(CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var yesterday = today.AddDays(-1);
        var cutoffUtc = yesterday.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        // ── Space-level post counts (Post → Discussion to get SpaceId) ─────────
        var spacePosts = await context.Posts
            .Where(p => !p.IsDeleted && p.CreatedAt >= cutoffUtc)
            .Join(context.Discussions.Where(d => !d.IsDeleted),
                  p => p.DiscussionId, d => d.Id,
                  (p, d) => new { p.CreatedAt, d.SpaceId })
            .GroupBy(x => new { DateVal = x.CreatedAt.Date, x.SpaceId })
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

        // ── Upsert per-day ────────────────────────────────────────────────────
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

                await UpsertCoreAsync(date, ActivityEntityTypeEnum.Space, spaceId, p, d, ct);

                if (!spaceToHub.TryGetValue(spaceId, out var hubId)) continue;
                hubPosts[hubId]        = hubPosts.GetValueOrDefault(hubId) + p;
                hubDiscussions[hubId]  = hubDiscussions.GetValueOrDefault(hubId) + d;

                if (!hubToCommunity.TryGetValue(hubId, out var communityId)) continue;
                communityPosts[communityId]        = communityPosts.GetValueOrDefault(communityId) + p;
                communityDiscussions[communityId]  = communityDiscussions.GetValueOrDefault(communityId) + d;
            }

            foreach (var (hubId, p) in hubPosts)
                await UpsertCoreAsync(date, ActivityEntityTypeEnum.Hub, hubId, p, hubDiscussions.GetValueOrDefault(hubId), ct);

            foreach (var (communityId, p) in communityPosts)
                await UpsertCoreAsync(date, ActivityEntityTypeEnum.Community, communityId, p, communityDiscussions.GetValueOrDefault(communityId), ct);

            var totalPosts       = communityPosts.Values.Sum();
            var totalDiscussions = communityDiscussions.Values.Sum();
            if (totalPosts > 0 || totalDiscussions > 0)
                await UpsertCoreAsync(date, ActivityEntityTypeEnum.Platform, 0, totalPosts, totalDiscussions, ct);

            // ── Discussion sparklines ───────────────────────────────────────────
            foreach (var x in discussionPosts.Where(x => x.DateVal == dateTime))
                await UpsertCoreAsync(date, ActivityEntityTypeEnum.Discussion, x.DiscussionId, x.Count, 0, ct);

            // ── User sparklines ─────────────────────────────────────────────────
            foreach (var x in userPosts.Where(x => x.DateVal == dateTime))
            {
                var d = userDiscussions.FirstOrDefault(u => u.DateVal == dateTime && u.CreatedByUserId == x.CreatedByUserId)?.Count ?? 0;
                await UpsertCoreAsync(date, ActivityEntityTypeEnum.User, x.CreatedByUserId, x.Count, d, ct);
            }
            foreach (var x in userDiscussions.Where(x => x.DateVal == dateTime && !userPosts.Any(p => p.DateVal == dateTime && p.CreatedByUserId == x.CreatedByUserId)))
                await UpsertCoreAsync(date, ActivityEntityTypeEnum.User, x.CreatedByUserId, 0, x.Count, ct);
        }
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

    private async Task UpsertCoreAsync(DateOnly date, ActivityEntityTypeEnum entityType, int entityId, int postCount, int discussionCount, CancellationToken ct)
    {
        var entityTypeInt = (int)entityType;

        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "ActivityDailySnapshot" ("Date", "EntityType", "EntityId", "PostCount", "DiscussionCount")
            VALUES ({date}, {entityTypeInt}, {entityId}, {postCount}, {discussionCount})
            ON CONFLICT ("Date", "EntityType", "EntityId")
            DO UPDATE SET
                "PostCount"       = EXCLUDED."PostCount",
                "DiscussionCount" = EXCLUDED."DiscussionCount"
            """, ct);
    }

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
