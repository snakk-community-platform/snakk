namespace Snakk.Infrastructure.Adapters;

using Microsoft.EntityFrameworkCore;
using Snakk.Domain.Entities;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;

public class DiscussionReadStateRepositoryAdapter(SnakkDbContext dbContext) : IDiscussionReadStateRepository
{
    public async Task<DiscussionReadState?> GetAsync(UserId userId, DiscussionId discussionId)
    {
        var entity = await dbContext.DiscussionReadStates
            .FirstOrDefaultAsync(rs =>
                rs.UserId == userId.Value
                && rs.DiscussionId == discussionId.Value);

        if (entity is null)
            return null;

        return DiscussionReadState.Rehydrate(
            UserId.From(entity.UserId),
            DiscussionId.From(entity.DiscussionId),
            entity.LastReadPostId is not null ? PostId.From(entity.LastReadPostId) : null,
            entity.LastReadAt);
    }

    public async Task SaveAsync(DiscussionReadState readState)
    {
        var existing = await dbContext.DiscussionReadStates
            .FirstOrDefaultAsync(rs =>
                rs.UserId == readState.UserId.Value
                && rs.DiscussionId == readState.DiscussionId.Value);

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

        await dbContext.SaveChangesAsync();
    }

    public async Task BatchSaveAsync(IEnumerable<DiscussionReadState> readStates)
    {
        var states = readStates.ToList();
        if (states.Count == 0) return;

        var userId = states[0].UserId.Value;
        var discussionIds = states.Select(rs => rs.DiscussionId.Value).ToList();

        var existingEntities = await dbContext.DiscussionReadStates
            .Where(rs => rs.UserId == userId && discussionIds.Contains(rs.DiscussionId))
            .ToListAsync();

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

        await dbContext.SaveChangesAsync();
    }

    public async Task<List<ReadStateWithPostNumber>> GetReadStatesForDiscussionsAsync(
        UserId userId,
        List<string> discussionIds)
    {
        var readStates = await dbContext.DiscussionReadStates
            .Where(rs =>
                rs.UserId == userId.Value
                && discussionIds.Contains(rs.DiscussionId))
            .ToListAsync();

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
                .FirstOrDefaultAsync();

            if (lastReadPost == default)
                continue;

            var postNumber = await dbContext.Posts
                .Where(p =>
                    p.Discussion.PublicId == readState.DiscussionId
                    && !p.IsDeleted
                    && p.CreatedAt <= lastReadPost)
                .CountAsync();

            results.Add(new ReadStateWithPostNumber(readState.DiscussionId, postNumber));
        }

        return results;
    }
}
