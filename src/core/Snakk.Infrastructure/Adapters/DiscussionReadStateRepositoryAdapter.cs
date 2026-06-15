namespace Snakk.Infrastructure.Adapters;

using Microsoft.EntityFrameworkCore;
using Snakk.Domain.Entities;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;

public class DiscussionReadStateRepositoryAdapter(SnakkDbContext dbContext) : IDiscussionReadStateRepository
{
    public async Task<DiscussionReadState?> GetAsync(UserId userId, DiscussionId discussionId, CancellationToken ct = default)
    {
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
        // AsTracking() so the LastReadPostId / LastReadAt mutations on the existing
        // row actually persist. Without this, EF's default NoTracking behavior on
        // SnakkDbContext silently dropped every update past the first row insert,
        // leaving the unread indicator stuck at the first-ever read position (CR-26).
        var existing = await dbContext.DiscussionReadStates
            .AsTracking()
            .FirstOrDefaultAsync(rs =>
                rs.UserId == readState.UserId.Value
                && rs.DiscussionId == readState.DiscussionId.Value, ct);

        if (existing is not null)
        {
            existing.LastReadPostId = readState.LastReadPostId?.Value;
            existing.LastReadAt = readState.LastReadAt;
        }
        else
        {
            var entity = new DiscussionReadStateDatabaseEntity
            {
                UserId = readState.UserId.Value,
                DiscussionId = readState.DiscussionId.Value,
                LastReadPostId = readState.LastReadPostId?.Value,
                LastReadAt = readState.LastReadAt
            };
            dbContext.DiscussionReadStates.Add(entity);
        }

        await dbContext.SaveChangesAsync(ct);
    }

    public async Task BatchSaveAsync(IEnumerable<DiscussionReadState> readStates, CancellationToken ct = default)
    {
        var states = readStates.ToList();
        if (states.Count == 0) return;

        var userId = states[0].UserId.Value;
        var discussionIds = states.Select(rs => rs.DiscussionId.Value).ToList();

        var existingEntities = await dbContext.DiscussionReadStates
            .AsTracking()
            .Where(rs => rs.UserId == userId && discussionIds.Contains(rs.DiscussionId))
            .ToListAsync(ct);

        var existingMap = existingEntities.ToDictionary(rs => rs.DiscussionId);

        foreach (var readState in states)
        {
            if (existingMap.TryGetValue(readState.DiscussionId.Value, out var existing))
            {
                existing.LastReadPostId = readState.LastReadPostId?.Value;
                existing.LastReadAt = readState.LastReadAt;
            }
            else
            {
                dbContext.DiscussionReadStates.Add(new DiscussionReadStateDatabaseEntity
                {
                    UserId = readState.UserId.Value,
                    DiscussionId = readState.DiscussionId.Value,
                    LastReadPostId = readState.LastReadPostId?.Value,
                    LastReadAt = readState.LastReadAt
                });
            }
        }

        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<List<ReadStateWithPostNumber>> GetReadStatesForDiscussionsAsync(
        UserId userId,
        List<string> discussionIds,
        CancellationToken ct = default)
    {
        var readStates = await dbContext.DiscussionReadStates
            .Where(rs =>
                rs.UserId == userId.Value
                && discussionIds.Contains(rs.DiscussionId))
            .ToListAsync(ct);

        var results = new List<ReadStateWithPostNumber>();

        foreach (var readState in readStates)
        {
            if (readState.LastReadPostId is null)
                continue;

            // Calculate post number by counting posts created before or at the LastReadPost
            var lastReadPost = await dbContext.Posts
                .Where(p =>
                    p.PublicId == readState.LastReadPostId
                    && p.Discussion.PublicId == readState.DiscussionId)
                .Select(p => p.CreatedAt)
                .FirstOrDefaultAsync(ct);

            if (lastReadPost == default)
                continue;

            var postNumber = await dbContext.Posts
                .Where(p =>
                    p.Discussion.PublicId == readState.DiscussionId
                    && !p.IsDeleted
                    && p.CreatedAt <= lastReadPost)
                .CountAsync(ct);

            results.Add(new ReadStateWithPostNumber(readState.DiscussionId, postNumber));
        }

        return results;
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
