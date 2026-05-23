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
    }

    public async Task PruneAsync(int retainDays, CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow.AddDays(-retainDays);
        await context.DiscussionViewSnapshots
            .Where(s => s.Hour < cutoff)
            .ExecuteDeleteAsync(ct);
    }
}
