using Microsoft.EntityFrameworkCore;
using Snakk.Application.Repositories;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;

namespace Snakk.Infrastructure.Database.Repositories;

public class StatsRollupRepository(SnakkDbContext context) : IStatsRollupRepository
{
    public async Task<List<StatsRollupRow>> GetRowsAsync(string statKind, CancellationToken ct = default) =>
        await context.StatsRollups
            .Where(r => r.StatKind == statKind)
            .OrderBy(r => r.Rank)
            .Select(r => new StatsRollupRow(r.StatKind, r.Rank, r.PayloadJson, r.ComputedAt))
            .ToListAsync(ct);

    public async Task ReplaceKindAsync(string statKind, IReadOnlyList<StatsRollupRow> rows, CancellationToken ct = default)
    {
        // Delete existing rows for this kind
        var existing = await context.StatsRollups
            .Where(r => r.StatKind == statKind)
            .ToListAsync(ct);

        context.StatsRollups.RemoveRange(existing);

        // Insert new rows
        var entities = rows.Select(r => new StatsRollupDatabaseEntity
        {
            StatKind = r.StatKind,
            Rank = r.Rank,
            PayloadJson = r.PayloadJson,
            ComputedAt = r.ComputedAt
        });

        await context.StatsRollups.AddRangeAsync(entities, ct);
        await context.SaveChangesAsync(ct);
    }
}
