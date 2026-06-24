namespace Snakk.Infrastructure.Database.Repositories;

using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using Snakk.Application.Repositories;
using Snakk.Infrastructure.EventHandlers.Trending;

public class CounterRepository(IDbContextFactory<SnakkDbContext> dbFactory, IConnectionMultiplexer redis) : ICounterRepository
{
    private const string PostCountKeyPrefix = "snakk:counter:post:discussion:";
    private const string PostCountKeyPattern = "snakk:counter:post:discussion:*";

    private const string FollowerSpaceKeyPrefix = "snakk:counter:follower:space:";
    private const string FollowerSpaceKeyPattern = "snakk:counter:follower:space:*";
    private const string FollowerDiscussionKeyPrefix = "snakk:counter:follower:discussion:";
    private const string FollowerDiscussionKeyPattern = "snakk:counter:follower:discussion:*";
    private const string FollowerUserKeyPrefix = "snakk:counter:follower:user:";
    private const string FollowerUserKeyPattern = "snakk:counter:follower:user:*";
    private const string UserDiscussionsKeyPrefix = "snakk:counter:user-discussions:";
    private const string UserDiscussionsKeyPattern = "snakk:counter:user-discussions:*";
    private const string UserRepliesKeyPrefix = "snakk:counter:user-replies:";
    private const string UserRepliesKeyPattern = "snakk:counter:user-replies:*";

    private const string DiscussionSpaceKeyPrefix = "snakk:counter:discussions:space:";
    private const string DiscussionSpaceKeyPattern = "snakk:counter:discussions:space:*";
    private const string ReactionPostKeyPrefix = "snakk:counter:reaction:post:";
    private const string ReactionPostKeyPattern = "snakk:counter:reaction:post:*";

    public async Task FlushPostCountsAsync(CancellationToken ct = default)
    {
        var db = redis.GetDatabase();
        var server = redis.GetServer(redis.GetEndPoints()[0]);

        var keys = server.Keys(pattern: PostCountKeyPattern).ToArray();
        if (keys.Length == 0) return;

        var batch = db.CreateBatch();
        var tasks = keys.Select(k => batch.StringGetDeleteAsync(k)).ToArray();
        batch.Execute();
        await Task.WhenAll(tasks);

        var publicIds = new List<string>(keys.Length);
        var deltas = new List<long>(keys.Length);
        for (var i = 0; i < keys.Length; i++)
        {
            var raw = (string?)await tasks[i];
            if (raw is null || !long.TryParse(raw, out var delta) || delta == 0) continue;
            publicIds.Add(((string)keys[i])[PostCountKeyPrefix.Length..]);
            deltas.Add(delta);
        }

        if (publicIds.Count == 0) return;

        var pidArray = publicIds.ToArray();
        var deltaArray = deltas.ToArray();

        await using var ctx = await dbFactory.CreateDbContextAsync(ct);

        // Discussion: apply delta and recompute EngagementScore = (PostCount + delta) + ReactionCount
        await ctx.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "Discussion" d
            SET "PostCount"       = d."PostCount" + v.delta,
                "EngagementScore" = (d."PostCount" + v.delta) + d."ReactionCount"
            FROM (SELECT UNNEST({pidArray}::text[]) AS pid, UNNEST({deltaArray}::bigint[]) AS delta) v
            WHERE d."PublicId" = v.pid
            """, ct);

        // Space: sum deltas across discussions belonging to the same space
        await ctx.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "Space" s
            SET "PostCount" = s."PostCount" + sub.delta
            FROM (
                SELECT d."SpaceId", SUM(v.delta) AS delta
                FROM (SELECT UNNEST({pidArray}::text[]) AS pid, UNNEST({deltaArray}::bigint[]) AS delta) v
                JOIN "Discussion" d ON d."PublicId" = v.pid
                GROUP BY d."SpaceId"
            ) sub
            WHERE s."Id" = sub."SpaceId"
            """, ct);

        // Hub
        await ctx.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "Hub" h
            SET "PostCount" = h."PostCount" + sub.delta
            FROM (
                SELECT d."HubId", SUM(v.delta) AS delta
                FROM (SELECT UNNEST({pidArray}::text[]) AS pid, UNNEST({deltaArray}::bigint[]) AS delta) v
                JOIN "Discussion" d ON d."PublicId" = v.pid
                GROUP BY d."HubId"
            ) sub
            WHERE h."Id" = sub."HubId"
            """, ct);

        // Community
        await ctx.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "Community" c
            SET "PostCount" = c."PostCount" + sub.delta
            FROM (
                SELECT d."CommunityId", SUM(v.delta) AS delta
                FROM (SELECT UNNEST({pidArray}::text[]) AS pid, UNNEST({deltaArray}::bigint[]) AS delta) v
                JOIN "Discussion" d ON d."PublicId" = v.pid
                GROUP BY d."CommunityId"
            ) sub
            WHERE c."Id" = sub."CommunityId"
            """, ct);
    }

    public async Task FlushFollowerCountsAsync(CancellationToken ct = default)
    {
        await using var ctx = await dbFactory.CreateDbContextAsync(ct);

        var (spaceIds, spaceDeltas) = await GetdelAllAsync(FollowerSpaceKeyPattern, FollowerSpaceKeyPrefix);
        if (spaceIds.Length > 0)
        {
            await ctx.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE "Space" s
                SET "FollowerCount" = s."FollowerCount" + v.delta
                FROM (SELECT UNNEST({spaceIds}::text[]) AS pid, UNNEST({spaceDeltas}::bigint[]) AS delta) v
                WHERE s."PublicId" = v.pid
                """, ct);
        }

        var (discIds, discDeltas) = await GetdelAllAsync(FollowerDiscussionKeyPattern, FollowerDiscussionKeyPrefix);
        if (discIds.Length > 0)
        {
            await ctx.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE "Discussion" d
                SET "FollowerCount" = d."FollowerCount" + v.delta
                FROM (SELECT UNNEST({discIds}::text[]) AS pid, UNNEST({discDeltas}::bigint[]) AS delta) v
                WHERE d."PublicId" = v.pid
                """, ct);
        }

        var (userIds, userDeltas) = await GetdelAllAsync(FollowerUserKeyPattern, FollowerUserKeyPrefix);
        if (userIds.Length > 0)
        {
            await ctx.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE "User" u
                SET "FollowerCount" = u."FollowerCount" + v.delta
                FROM (SELECT UNNEST({userIds}::text[]) AS pid, UNNEST({userDeltas}::bigint[]) AS delta) v
                WHERE u."PublicId" = v.pid
                """, ct);
        }
    }

    public async Task FlushUserCountsAsync(CancellationToken ct = default)
    {
        await using var ctx = await dbFactory.CreateDbContextAsync(ct);

        var (discIds, discDeltas) = await GetdelAllAsync(UserDiscussionsKeyPattern, UserDiscussionsKeyPrefix);
        if (discIds.Length > 0)
        {
            await ctx.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE "User" u
                SET "DiscussionCount" = u."DiscussionCount" + v.delta
                FROM (SELECT UNNEST({discIds}::text[]) AS pid, UNNEST({discDeltas}::bigint[]) AS delta) v
                WHERE u."PublicId" = v.pid
                """, ct);
        }

        var (replyIds, replyDeltas) = await GetdelAllAsync(UserRepliesKeyPattern, UserRepliesKeyPrefix);
        if (replyIds.Length > 0)
        {
            await ctx.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE "User" u
                SET "ReplyCount" = u."ReplyCount" + v.delta
                FROM (SELECT UNNEST({replyIds}::text[]) AS pid, UNNEST({replyDeltas}::bigint[]) AS delta) v
                WHERE u."PublicId" = v.pid
                """, ct);
        }
    }

    public async Task FlushDiscussionCountsAsync(CancellationToken ct = default)
    {
        var (spaceIds, deltas) = await GetdelAllAsync(DiscussionSpaceKeyPattern, DiscussionSpaceKeyPrefix);
        if (spaceIds.Length == 0) return;

        await using var ctx = await dbFactory.CreateDbContextAsync(ct);

        await ctx.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "Space" s
            SET "DiscussionCount" = s."DiscussionCount" + v.delta
            FROM (SELECT UNNEST({spaceIds}::text[]) AS pid, UNNEST({deltas}::bigint[]) AS delta) v
            WHERE s."PublicId" = v.pid
            """, ct);

        await ctx.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "Hub" h
            SET "DiscussionCount" = h."DiscussionCount" + sub.delta
            FROM (
                SELECT sp."HubId", SUM(v.delta) AS delta
                FROM (SELECT UNNEST({spaceIds}::text[]) AS pid, UNNEST({deltas}::bigint[]) AS delta) v
                JOIN "Space" sp ON sp."PublicId" = v.pid
                GROUP BY sp."HubId"
            ) sub
            WHERE h."Id" = sub."HubId"
            """, ct);

        await ctx.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "Community" c
            SET "DiscussionCount" = c."DiscussionCount" + sub.delta
            FROM (
                SELECT h."CommunityId", SUM(v.delta) AS delta
                FROM (SELECT UNNEST({spaceIds}::text[]) AS pid, UNNEST({deltas}::bigint[]) AS delta) v
                JOIN "Space" sp ON sp."PublicId" = v.pid
                JOIN "Hub" h ON h."Id" = sp."HubId"
                GROUP BY h."CommunityId"
            ) sub
            WHERE c."Id" = sub."CommunityId"
            """, ct);
    }

    public async Task FlushReactionCountsAsync(CancellationToken ct = default)
    {
        var (postIds, deltas) = await GetdelAllAsync(ReactionPostKeyPattern, ReactionPostKeyPrefix);
        if (postIds.Length == 0) return;

        await using var ctx = await dbFactory.CreateDbContextAsync(ct);

        await ctx.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "Post" p
            SET "ReactionCount" = p."ReactionCount" + v.delta
            FROM (SELECT UNNEST({postIds}::text[]) AS pid, UNNEST({deltas}::bigint[]) AS delta) v
            WHERE p."PublicId" = v.pid
            """, ct);

        await ctx.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "Discussion" d
            SET "ReactionCount"   = d."ReactionCount" + sub.delta,
                "EngagementScore" = d."PostCount" + (d."ReactionCount" + sub.delta)
            FROM (
                SELECT p."DiscussionId", SUM(v.delta) AS delta
                FROM (SELECT UNNEST({postIds}::text[]) AS pid, UNNEST({deltas}::bigint[]) AS delta) v
                JOIN "Post" p ON p."PublicId" = v.pid
                GROUP BY p."DiscussionId"
            ) sub
            WHERE d."Id" = sub."DiscussionId"
            """, ct);

        await ctx.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "Space" s
            SET "ReactionCount" = s."ReactionCount" + sub.delta
            FROM (
                SELECT d."SpaceId", SUM(v.delta) AS delta
                FROM (SELECT UNNEST({postIds}::text[]) AS pid, UNNEST({deltas}::bigint[]) AS delta) v
                JOIN "Post" p ON p."PublicId" = v.pid
                JOIN "Discussion" d ON d."Id" = p."DiscussionId"
                GROUP BY d."SpaceId"
            ) sub
            WHERE s."Id" = sub."SpaceId"
            """, ct);

        await ctx.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "Hub" h
            SET "ReactionCount" = h."ReactionCount" + sub.delta
            FROM (
                SELECT d."HubId", SUM(v.delta) AS delta
                FROM (SELECT UNNEST({postIds}::text[]) AS pid, UNNEST({deltas}::bigint[]) AS delta) v
                JOIN "Post" p ON p."PublicId" = v.pid
                JOIN "Discussion" d ON d."Id" = p."DiscussionId"
                GROUP BY d."HubId"
            ) sub
            WHERE h."Id" = sub."HubId"
            """, ct);

        await ctx.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "Community" c
            SET "ReactionCount" = c."ReactionCount" + sub.delta
            FROM (
                SELECT d."CommunityId", SUM(v.delta) AS delta
                FROM (SELECT UNNEST({postIds}::text[]) AS pid, UNNEST({deltas}::bigint[]) AS delta) v
                JOIN "Post" p ON p."PublicId" = v.pid
                JOIN "Discussion" d ON d."Id" = p."DiscussionId"
                GROUP BY d."CommunityId"
            ) sub
            WHERE c."Id" = sub."CommunityId"
            """, ct);
    }

    public async Task FlushTrendScoresAsync(CancellationToken ct = default)
    {
        var db = redis.GetDatabase();
        var dirty = await db.SetPopAsync(PostCreatedTrendingHandler.TrendDirtyKey, 1000);
        if (dirty.Length == 0) return;

        await using var ctx = await dbFactory.CreateDbContextAsync(ct);
        foreach (var id in dirty)
            await TrendScoreCalculator.RecalculateAsync(ctx, (string)id!, window: null, ct);
    }

    private const string ReadStateKeyPrefix = "snakk:readstate:";
    private const string ReadStateDirtyKey  = "snakk:readstate:dirty";

    public async Task FlushReadStatesAsync(CancellationToken ct = default)
    {
        var db = redis.GetDatabase();
        var dirty = await db.SetPopAsync(ReadStateDirtyKey, 5000);
        if (dirty.Length == 0) return;

        var members = dirty.Select(m => (string)m!).ToArray();

        // Batch GETDEL — atomically fetch and remove each value key
        var batch = db.CreateBatch();
        var tasks = members.Select(m => batch.StringGetDeleteAsync(ReadStateKeyPrefix + m)).ToArray();
        batch.Execute();
        await Task.WhenAll(tasks);

        var userIds = new List<string>(members.Length);
        var discIds = new List<string>(members.Length);
        var postIds = new List<string?>(members.Length);
        var readAts = new List<DateTime>(members.Length);

        for (var i = 0; i < members.Length; i++)
        {
            var raw = (string?)await tasks[i];
            if (raw is null) continue; // Race: key already gone (concurrent flush) — skip

            var sep = raw.IndexOf('|');
            if (sep < 0 || !long.TryParse(raw.AsSpan(sep + 1), out var ticks)) continue;

            var parts = members[i].Split(':', 2);
            if (parts.Length != 2) continue;

            userIds.Add(parts[0]);
            discIds.Add(parts[1]);
            postIds.Add(sep == 0 ? null : raw[..sep]);
            readAts.Add(new DateTime(ticks, DateTimeKind.Utc));
        }

        if (userIds.Count == 0) return;

        var uArr = userIds.ToArray();
        var dArr = discIds.ToArray();
        var pArr = postIds.ToArray();
        var rArr = readAts.ToArray();

        await using var ctx = await dbFactory.CreateDbContextAsync(ct);
        await ctx.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "DiscussionReadState" ("UserId", "DiscussionId", "LastReadPostId", "LastReadAt")
            SELECT v.uid, v.did, v.pid, v.rat
            FROM (
                SELECT UNNEST({uArr}::text[])        AS uid,
                       UNNEST({dArr}::text[])        AS did,
                       UNNEST({pArr}::text[])        AS pid,
                       UNNEST({rArr}::timestamptz[]) AS rat
            ) v
            ON CONFLICT ("UserId", "DiscussionId")
            DO UPDATE SET
                "LastReadPostId" = EXCLUDED."LastReadPostId",
                "LastReadAt"     = EXCLUDED."LastReadAt"
            """, ct);
    }

    private async Task<(string[] ids, long[] deltas)> GetdelAllAsync(string pattern, string prefix)
    {
        var db = redis.GetDatabase();
        var server = redis.GetServer(redis.GetEndPoints()[0]);
        var keys = server.Keys(pattern: pattern).ToArray();
        if (keys.Length == 0) return ([], []);

        var batch = db.CreateBatch();
        var tasks = keys.Select(k => batch.StringGetDeleteAsync(k)).ToArray();
        batch.Execute();
        await Task.WhenAll(tasks);

        var ids = new List<string>(keys.Length);
        var deltas = new List<long>(keys.Length);
        for (var i = 0; i < keys.Length; i++)
        {
            var raw = (string?)await tasks[i];
            if (raw is null || !long.TryParse(raw, out var delta) || delta == 0) continue;
            ids.Add(((string)keys[i])[prefix.Length..]);
            deltas.Add(delta);
        }
        return (ids.ToArray(), deltas.ToArray());
    }
}
