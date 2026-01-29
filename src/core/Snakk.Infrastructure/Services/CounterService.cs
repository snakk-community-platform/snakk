namespace Snakk.Infrastructure.Services;

using Microsoft.EntityFrameworkCore;
using Snakk.Application.Services;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Database;

/// <summary>
/// Service to update denormalized counts across the hierarchy using atomic SQL updates.
/// </summary>
public class CounterService(SnakkDbContext dbContext) : ICounterService
{
    public async Task IncrementDiscussionCountAsync(SpaceId spaceId)
    {
        // Get the space to find its hub and community
        var space = await dbContext.Spaces
            .AsNoTracking()
            .Where(s => s.PublicId == spaceId.Value)
            .Select(s => new { s.Id, s.HubId, HubCommunityId = s.Hub.CommunityId })
            .FirstOrDefaultAsync();

        if (space == null) return;

        // Increment Space.DiscussionCount
        await dbContext.Spaces
            .Where(s => s.Id == space.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.DiscussionCount, x => x.DiscussionCount + 1));

        // Increment Hub.DiscussionCount
        await dbContext.Hubs
            .Where(h => h.Id == space.HubId)
            .ExecuteUpdateAsync(h => h.SetProperty(x => x.DiscussionCount, x => x.DiscussionCount + 1));

        // Increment Community.DiscussionCount
        await dbContext.Communities
            .Where(c => c.Id == space.HubCommunityId)
            .ExecuteUpdateAsync(c => c.SetProperty(x => x.DiscussionCount, x => x.DiscussionCount + 1));
    }

    public async Task DecrementDiscussionCountAsync(SpaceId spaceId)
    {
        var space = await dbContext.Spaces
            .AsNoTracking()
            .Where(s => s.PublicId == spaceId.Value)
            .Select(s => new { s.Id, s.HubId, HubCommunityId = s.Hub.CommunityId })
            .FirstOrDefaultAsync();

        if (space == null) return;

        await dbContext.Spaces
            .Where(s => s.Id == space.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.DiscussionCount, x => x.DiscussionCount - 1));

        await dbContext.Hubs
            .Where(h => h.Id == space.HubId)
            .ExecuteUpdateAsync(h => h.SetProperty(x => x.DiscussionCount, x => x.DiscussionCount - 1));

        await dbContext.Communities
            .Where(c => c.Id == space.HubCommunityId)
            .ExecuteUpdateAsync(c => c.SetProperty(x => x.DiscussionCount, x => x.DiscussionCount - 1));
    }

    public async Task IncrementPostCountAsync(DiscussionId discussionId)
    {
        // Get discussion with its space, hub, and community ids
        var discussion = await dbContext.Discussions
            .AsNoTracking()
            .Where(d => d.PublicId == discussionId.Value)
            .Select(d => new { 
                d.Id, 
                d.SpaceId, 
                HubId = d.Space.HubId, 
                CommunityId = d.Space.Hub.CommunityId 
            })
            .FirstOrDefaultAsync();

        if (discussion == null) return;

        // Increment Discussion.PostCount
        await dbContext.Discussions
            .Where(d => d.Id == discussion.Id)
            .ExecuteUpdateAsync(d => d.SetProperty(x => x.PostCount, x => x.PostCount + 1));

        // Increment Space.PostCount
        await dbContext.Spaces
            .Where(s => s.Id == discussion.SpaceId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.PostCount, x => x.PostCount + 1));

        // Increment Hub.PostCount
        await dbContext.Hubs
            .Where(h => h.Id == discussion.HubId)
            .ExecuteUpdateAsync(h => h.SetProperty(x => x.PostCount, x => x.PostCount + 1));

        // Increment Community.PostCount
        await dbContext.Communities
            .Where(c => c.Id == discussion.CommunityId)
            .ExecuteUpdateAsync(c => c.SetProperty(x => x.PostCount, x => x.PostCount + 1));
    }

    public async Task DecrementPostCountAsync(DiscussionId discussionId)
    {
        var discussion = await dbContext.Discussions
            .AsNoTracking()
            .Where(d => d.PublicId == discussionId.Value)
            .Select(d => new { 
                d.Id, 
                d.SpaceId, 
                HubId = d.Space.HubId, 
                CommunityId = d.Space.Hub.CommunityId 
            })
            .FirstOrDefaultAsync();

        if (discussion == null) return;

        await dbContext.Discussions
            .Where(d => d.Id == discussion.Id)
            .ExecuteUpdateAsync(d => d.SetProperty(x => x.PostCount, x => x.PostCount - 1));

        await dbContext.Spaces
            .Where(s => s.Id == discussion.SpaceId)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.PostCount, x => x.PostCount - 1));

        await dbContext.Hubs
            .Where(h => h.Id == discussion.HubId)
            .ExecuteUpdateAsync(h => h.SetProperty(x => x.PostCount, x => x.PostCount - 1));

        await dbContext.Communities
            .Where(c => c.Id == discussion.CommunityId)
            .ExecuteUpdateAsync(c => c.SetProperty(x => x.PostCount, x => x.PostCount - 1));
    }

    public async Task IncrementUniqueReactorCountAsync(DiscussionId discussionId, UserId userId)
    {
        // Get discussion entity
        var discussion = await dbContext.Discussions
            .AsNoTracking()
            .Where(d => d.PublicId == discussionId.Value)
            .Select(d => new { d.Id })
            .FirstOrDefaultAsync();

        if (discussion == null) return;

        // Check if user has ANY existing reactions in this discussion
        var hasExistingReaction = await dbContext.Reactions
            .AsNoTracking()
            .AnyAsync(r => r.Post.DiscussionId == discussion.Id && r.User.PublicId == userId.Value);

        // Only increment if this is their FIRST reaction in the discussion
        if (!hasExistingReaction)
        {
            await dbContext.Discussions
                .Where(d => d.Id == discussion.Id)
                .ExecuteUpdateAsync(d => d.SetProperty(x => x.ReactionCount, x => x.ReactionCount + 1));
        }
    }

    public async Task DecrementUniqueReactorCountAsync(DiscussionId discussionId, UserId userId)
    {
        // Get discussion entity
        var discussion = await dbContext.Discussions
            .AsNoTracking()
            .Where(d => d.PublicId == discussionId.Value)
            .Select(d => new { d.Id })
            .FirstOrDefaultAsync();

        if (discussion == null) return;

        // Check if user has ANY remaining reactions in this discussion
        var hasRemainingReactions = await dbContext.Reactions
            .AsNoTracking()
            .AnyAsync(r => r.Post.DiscussionId == discussion.Id && r.User.PublicId == userId.Value);

        // Only decrement if they have NO MORE reactions in the discussion
        if (!hasRemainingReactions)
        {
            await dbContext.Discussions
                .Where(d => d.Id == discussion.Id)
                .ExecuteUpdateAsync(d => d.SetProperty(x => x.ReactionCount, x => x.ReactionCount - 1));
        }
    }
}
