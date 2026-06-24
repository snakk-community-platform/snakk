namespace Snakk.Infrastructure.Database.Repositories;

using Microsoft.EntityFrameworkCore;
using Snakk.Application.Repositories;
using Snakk.Infrastructure.Database;

public class DiscussionViewRepository(SnakkDbContext context) : IDiscussionViewRepository
{
    public async Task FlushViewsAsync(
        IReadOnlyDictionary<(string DiscussionPublicId, string CountryCode), long> counts,
        CancellationToken ct = default)
    {
        if (counts.Count == 0) return;

        var hour = new DateTime(DateTime.UtcNow.Ticks / TimeSpan.TicksPerHour * TimeSpan.TicksPerHour, DateTimeKind.Utc);

        var publicIds    = counts.Keys.Select(k => k.DiscussionPublicId).ToArray();
        var countryCodes = counts.Keys.Select(k => k.CountryCode).ToArray();
        var viewCounts   = counts.Values.ToArray();
        var hours        = Enumerable.Repeat(hour, counts.Count).ToArray();

        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "DiscussionViewSnapshot" ("Hour", "DiscussionPublicId", "CountryCode", "ViewCount")
            SELECT UNNEST({hours}), UNNEST({publicIds}), UNNEST({countryCodes}), UNNEST({viewCounts})
            ON CONFLICT ("Hour", "DiscussionPublicId", "CountryCode")
            DO UPDATE SET "ViewCount" = "DiscussionViewSnapshot"."ViewCount" + EXCLUDED."ViewCount"
            """, ct);

        // Roll up to denormalized total on Discussion
        var byDiscussion = counts
            .GroupBy(kv => kv.Key.DiscussionPublicId)
            .Select(g => (PublicId: g.Key, Delta: g.Sum(kv => kv.Value)))
            .ToList();

        var discussionIds    = byDiscussion.Select(x => x.PublicId).ToArray();
        var discussionDeltas = byDiscussion.Select(x => x.Delta).ToArray();

        await context.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "Discussion" AS d
            SET "ViewCount" = d."ViewCount" + v.delta
            FROM (SELECT UNNEST({discussionIds}) AS pid, UNNEST({discussionDeltas}) AS delta) AS v
            WHERE d."PublicId" = v.pid
            """, ct);
    }

    public async Task PruneAsync(int retainDays, CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-retainDays);
        await context.DiscussionViewSnapshots
            .Where(s => s.Hour < cutoff)
            .ExecuteDeleteAsync(ct);
    }

    public async Task RollupViewCountsAsync(CancellationToken ct = default)
    {
        await context.Database.ExecuteSqlRawAsync("""
            UPDATE "Space" s
            SET "ViewCount" = COALESCE(sub.total, 0)
            FROM (
                SELECT "SpaceId", SUM("ViewCount") AS total
                FROM "Discussion"
                WHERE "IsDeleted" = FALSE
                GROUP BY "SpaceId"
            ) sub
            WHERE s."Id" = sub."SpaceId"
            """, ct);

        await context.Database.ExecuteSqlRawAsync("""
            UPDATE "Hub" h
            SET "ViewCount" = COALESCE(sub.total, 0)
            FROM (
                SELECT "HubId", SUM("ViewCount") AS total
                FROM "Discussion"
                WHERE "IsDeleted" = FALSE
                GROUP BY "HubId"
            ) sub
            WHERE h."Id" = sub."HubId"
            """, ct);

        await context.Database.ExecuteSqlRawAsync("""
            UPDATE "Community" c
            SET "ViewCount" = COALESCE(sub.total, 0)
            FROM (
                SELECT "CommunityId", SUM("ViewCount") AS total
                FROM "Discussion"
                WHERE "IsDeleted" = FALSE
                GROUP BY "CommunityId"
            ) sub
            WHERE c."Id" = sub."CommunityId"
            """, ct);
    }
}
