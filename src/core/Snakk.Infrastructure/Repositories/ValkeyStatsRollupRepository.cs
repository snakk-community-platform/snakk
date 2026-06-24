namespace Snakk.Infrastructure.Repositories;

using Snakk.Application.Repositories;
using StackExchange.Redis;
using System.Text.Json;

public class ValkeyStatsRollupRepository(IConnectionMultiplexer redis) : IStatsRollupRepository
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    private static string Key(string statKind) => $"snakk:stats:{statKind}";

    public async Task<List<StatsRollupRow>> GetRowsAsync(string statKind, CancellationToken ct = default)
    {
        var db = redis.GetDatabase();
        var json = await db.StringGetAsync(Key(statKind));
        if (json.IsNullOrEmpty) return [];
        return JsonSerializer.Deserialize<List<StatsRollupRow>>((string)json!) ?? [];
    }

    public async Task ReplaceAllAsync(IReadOnlyList<StatsRollupRow> rows, CancellationToken ct = default)
    {
        var db = redis.GetDatabase();
        var batch = db.CreateBatch();
        var tasks = rows
            .GroupBy(r => r.StatKind)
            .Select(g => batch.StringSetAsync(Key(g.Key), JsonSerializer.Serialize(g.ToList()), Ttl))
            .ToList();
        batch.Execute();
        await Task.WhenAll(tasks);
    }
}
