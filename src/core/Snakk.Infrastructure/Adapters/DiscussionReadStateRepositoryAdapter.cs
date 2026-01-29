namespace Snakk.Infrastructure.Adapters;

using Microsoft.EntityFrameworkCore;
using Snakk.Domain.Entities;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Database;
using Snakk.Infrastructure.Database.Entities;

public class DiscussionReadStateRepositoryAdapter(SnakkDbContext dbContext) : IDiscussionReadStateRepository
{
    private readonly SnakkDbContext _dbContext = dbContext;

    public async Task<DiscussionReadState?> GetAsync(UserId userId, DiscussionId discussionId)
    {
        var entity = await _dbContext.DiscussionReadStates
            .FirstOrDefaultAsync(rs => rs.UserId == userId.Value && rs.DiscussionId == discussionId.Value);

        if (entity == null)
            return null;

        return DiscussionReadState.Rehydrate(
            UserId.From(entity.UserId),
            DiscussionId.From(entity.DiscussionId),
            entity.LastReadPostId != null ? PostId.From(entity.LastReadPostId) : null,
            entity.LastReadAt);
    }

    public async Task SaveAsync(DiscussionReadState readState)
    {
        var existing = await _dbContext.DiscussionReadStates
            .FirstOrDefaultAsync(rs =>
                rs.UserId == readState.UserId.Value &&
                rs.DiscussionId == readState.DiscussionId.Value);

        if (existing != null)
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
            _dbContext.DiscussionReadStates.Add(entity);
        }

        await _dbContext.SaveChangesAsync();
    }

    public async Task<List<ReadStateWithPostNumber>> GetReadStatesForDiscussionsAsync(
        UserId userId, List<string> discussionIds)
    {
        var readStates = await _dbContext.DiscussionReadStates
            .Where(rs => rs.UserId == userId.Value && discussionIds.Contains(rs.DiscussionId))
            .ToListAsync();

        var results = new List<ReadStateWithPostNumber>();

        foreach (var readState in readStates)
        {
            if (readState.LastReadPostId == null)
                continue;

            // Calculate post number by counting posts created before or at the LastReadPost
            var lastReadPost = await _dbContext.Posts
                .Where(p => p.PublicId == readState.LastReadPostId && p.Discussion.PublicId == readState.DiscussionId)
                .Select(p => p.CreatedAt)
                .FirstOrDefaultAsync();

            if (lastReadPost == default)
                continue;

            var postNumber = await _dbContext.Posts
                .Where(p => p.Discussion.PublicId == readState.DiscussionId &&
                           !p.IsDeleted &&
                           p.CreatedAt <= lastReadPost)
                .CountAsync();

            results.Add(new ReadStateWithPostNumber(readState.DiscussionId, postNumber));
        }

        return results;
    }
}
