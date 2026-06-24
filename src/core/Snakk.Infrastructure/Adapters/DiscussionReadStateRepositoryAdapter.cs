namespace Snakk.Infrastructure.Adapters;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using StackExchange.Redis;
using Snakk.Domain.Entities;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;

public class DiscussionReadStateRepositoryAdapter(
    SnakkDbContext dbContext,
    HybridCache cache,
    IConnectionMultiplexer redis) : IDiscussionReadStateRepository
{
    private static readonly HybridCacheEntryOptions _readStateCacheOptions =
        new() { Expiration = TimeSpan.FromHours(24) };

    internal const string DirtySetKey = "snakk:readstate:dirty";
    internal const string KeyPrefix    = "snakk:readstate:";

    public async Task<DiscussionReadState?> GetAsync(UserId userId, DiscussionId discussionId, CancellationToken ct = default)
    {
        return await cache.GetOrCreateAsync<DiscussionReadState?>(
            $"read-state:{userId.Value}:{discussionId.Value}",
            async cancel => await FetchReadStateAsync(userId, discussionId, cancel),
            _readStateCacheOptions,
            cancellationToken: ct);
    }

    private async Task<DiscussionReadState?> FetchReadStateAsync(UserId userId, DiscussionId discussionId, CancellationToken ct)
    {
        // HybridCache missed — check Valkey write-behind store before hitting DB.
        // Covers the window between a SaveAsync write and the hourly DB flush.
        var raw = (string?)await redis.GetDatabase()
            .StringGetAsync(KeyPrefix + userId.Value + ":" + discussionId.Value);
        if (raw is not null)
        {
            var pending = ParseValue(userId, discussionId, raw);
            if (pending is not null) return pending;
        }

        var entity = await dbContext.DiscussionReadStates
            .FirstOrDefaultAsync(rs =>
                rs.UserId == userId.Value
                && rs.DiscussionId == discussionId.Value, ct);

        if (entity is null)
            return null;

        return DiscussionReadState.Rehydrate(
            UserId.From(entity.UserId),
            DiscussionId.From(entity.DiscussionId),
            entity.LastReadPostId is not null ? PostId.From(entity.LastReadPostId) : null,
            entity.LastReadAt);
    }

    public async Task SaveAsync(DiscussionReadState readState, CancellationToken ct = default)
    {
        var db = redis.GetDatabase();
        var key    = KeyPrefix + readState.UserId.Value + ":" + readState.DiscussionId.Value;
        var member = readState.UserId.Value + ":" + readState.DiscussionId.Value;

        var batch = db.CreateBatch();
        var setTask  = batch.StringSetAsync(key, SerializeValue(readState));
        var saddTask = batch.SetAddAsync(DirtySetKey, member);
        batch.Execute();
        await Task.WhenAll(setTask, saddTask);

        // Populate HybridCache so GetAsync never touches DB on the hot path.
        await cache.SetAsync<DiscussionReadState?>(
            $"read-state:{readState.UserId.Value}:{readState.DiscussionId.Value}",
            readState,
            _readStateCacheOptions,
            cancellationToken: ct);
    }

    public async Task BatchSaveAsync(IEnumerable<DiscussionReadState> readStates, CancellationToken ct = default)
    {
        var states = readStates.ToList();
        if (states.Count == 0) return;

        var db = redis.GetDatabase();
        var batch = db.CreateBatch();
        var tasks = new List<Task>(states.Count * 2);

        foreach (var s in states)
        {
            var key    = KeyPrefix + s.UserId.Value + ":" + s.DiscussionId.Value;
            var member = s.UserId.Value + ":" + s.DiscussionId.Value;
            tasks.Add(batch.StringSetAsync(key, SerializeValue(s)));
            tasks.Add(batch.SetAddAsync(DirtySetKey, member));
        }

        batch.Execute();
        await Task.WhenAll(tasks);

        await Task.WhenAll(states.Select(s =>
            cache.SetAsync<DiscussionReadState?>(
                $"read-state:{s.UserId.Value}:{s.DiscussionId.Value}",
                s,
                _readStateCacheOptions,
                cancellationToken: ct).AsTask()));
    }

    private static string SerializeValue(DiscussionReadState s) =>
        (s.LastReadPostId?.Value ?? "") + "|" + s.LastReadAt.Ticks;

    private static DiscussionReadState? ParseValue(UserId userId, DiscussionId discussionId, string raw)
    {
        var sep = raw.IndexOf('|');
        if (sep < 0 || !long.TryParse(raw.AsSpan(sep + 1), out var ticks)) return null;

        var postIdStr = raw[..sep];
        return DiscussionReadState.Rehydrate(
            userId,
            discussionId,
            postIdStr.Length > 0 ? PostId.From(postIdStr) : null,
            new DateTime(ticks, DateTimeKind.Utc));
    }

    public async Task<List<ReadStateWithPostNumber>> GetReadStatesForDiscussionsAsync(
        UserId userId,
        List<string> discussionIds,
        CancellationToken ct = default)
    {
        // Single query: join read states to last-read post, then count posts up to that timestamp.
        // Replaces the prior N+1 pattern (1 SELECT + 2 queries per discussion row).
        var results = await (
            from rs in dbContext.DiscussionReadStates
            where rs.UserId == userId.Value
                  && discussionIds.Contains(rs.DiscussionId)
                  && rs.LastReadPostId != null
            join lrp in dbContext.Posts on rs.LastReadPostId equals lrp.PublicId
            where lrp.DiscussionPublicId == rs.DiscussionId
            select new
            {
                rs.DiscussionId,
                PostNumber = dbContext.Posts.Count(p =>
                    p.DiscussionPublicId == rs.DiscussionId
                    && !p.IsDeleted
                    && p.CreatedAt <= lrp.CreatedAt)
            }
        ).ToListAsync(ct);

        return results
            .Select(r => new ReadStateWithPostNumber(r.DiscussionId, r.PostNumber))
            .ToList();
    }

    public async Task<Dictionary<string, DateTime>> GetLastReadAtByDiscussionAsync(
        string userId,
        List<string> discussionIds,
        CancellationToken ct = default)
    {
        return await dbContext.DiscussionReadStates
            .Where(rs => rs.UserId == userId && discussionIds.Contains(rs.DiscussionId))
            .Select(rs => new { rs.DiscussionId, rs.LastReadAt })
            .ToDictionaryAsync(rs => rs.DiscussionId, rs => rs.LastReadAt, ct);
    }

    public async Task<Dictionary<string, int>> GetUnreadPostCountsAsync(
        Dictionary<string, DateTime> cutoffByDiscussionId,
        CancellationToken ct = default)
    {
        if (cutoffByDiscussionId.Count == 0) return [];

        var discussionIds = cutoffByDiscussionId.Keys.ToList();
        var minCutoff = cutoffByDiscussionId.Values.Min();

        // Single query: fetch post timestamps for all discussions after the earliest cutoff.
        // Then apply per-discussion cutoffs in memory (avoids N+1 queries).
        var rows = await dbContext.Posts
            .Where(p =>
                p.DiscussionPublicId != null
                && discussionIds.Contains(p.DiscussionPublicId)
                && !p.IsDeleted
                && p.CreatedAt > minCutoff)
            .Select(p => new { p.DiscussionPublicId, p.CreatedAt })
            .ToListAsync(ct);

        var result = new Dictionary<string, int>();
        foreach (var row in rows)
        {
            if (row.DiscussionPublicId is null) continue;
            if (!cutoffByDiscussionId.TryGetValue(row.DiscussionPublicId, out var cutoff)) continue;
            if (row.CreatedAt <= cutoff) continue;

            result.TryGetValue(row.DiscussionPublicId, out var count);
            result[row.DiscussionPublicId] = count + 1;
        }
        return result;
    }
}
