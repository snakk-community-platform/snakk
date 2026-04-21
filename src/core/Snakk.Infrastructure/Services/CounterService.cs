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
            .Where(s => s.PublicId == spaceId.Value)
            .Select(s => new {
                s.Id,
                s.HubId,
                s.Hub.CommunityId })
            .FirstOrDefaultAsync();

        if (space is null) return;

        // Increment Space.DiscussionCount
        await dbContext.Spaces
            .Where(s => s.Id == space.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(
                x => x.DiscussionCount,
                x => x.DiscussionCount + 1));

        // Increment Hub.DiscussionCount
        await dbContext.Hubs
            .Where(h => h.Id == space.HubId)
            .ExecuteUpdateAsync(h => h.SetProperty(
                x => x.DiscussionCount,
                x => x.DiscussionCount + 1));

        // Increment Community.DiscussionCount
        await dbContext.Communities
            .Where(c => c.Id == space.CommunityId)
            .ExecuteUpdateAsync(c => c.SetProperty(
                x => x.DiscussionCount,
                x => x.DiscussionCount + 1));
    }

    public async Task DecrementDiscussionCountAsync(SpaceId spaceId)
    {
        var space = await dbContext.Spaces
            .Where(s => s.PublicId == spaceId.Value)
            .Select(s => new {
                s.Id,
                s.HubId,
                s.Hub.CommunityId })
            .FirstOrDefaultAsync();

        if (space is null) return;

        await dbContext.Spaces
            .Where(s => s.Id == space.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(
                x => x.DiscussionCount,
                x => x.DiscussionCount - 1));

        await dbContext.Hubs
            .Where(h => h.Id == space.HubId)
            .ExecuteUpdateAsync(h => h.SetProperty(
                x => x.DiscussionCount,
                x => x.DiscussionCount - 1));

        await dbContext.Communities
            .Where(c => c.Id == space.CommunityId)
            .ExecuteUpdateAsync(c => c.SetProperty(
                x => x.DiscussionCount,
                x => x.DiscussionCount - 1));
    }

    public async Task IncrementPostCountAsync(DiscussionId discussionId)
    {
        // Get discussion with its space, hub, and community ids
        var discussion = await dbContext.Discussions
            .Where(d => d.PublicId == discussionId.Value)
            .Select(d => new {
                d.Id,
                d.SpaceId,
                d.Space.HubId,
                d.Space.Hub.CommunityId })
            .FirstOrDefaultAsync();

        if (discussion is null) return;

        // Increment Discussion.PostCount
        await dbContext.Discussions
            .Where(d => d.Id == discussion.Id)
            .ExecuteUpdateAsync(d => d.SetProperty(
                x => x.PostCount,
                x => x.PostCount + 1));

        // Increment Space.PostCount
        await dbContext.Spaces
            .Where(s => s.Id == discussion.SpaceId)
            .ExecuteUpdateAsync(s => s.SetProperty(
                x => x.PostCount,
                x => x.PostCount + 1));

        // Increment Hub.PostCount
        await dbContext.Hubs
            .Where(h => h.Id == discussion.HubId)
            .ExecuteUpdateAsync(h => h.SetProperty(
                x => x.PostCount,
                x => x.PostCount + 1));

        // Increment Community.PostCount
        await dbContext.Communities
            .Where(c => c.Id == discussion.CommunityId)
            .ExecuteUpdateAsync(c => c.SetProperty(
                x => x.PostCount,
                x => x.PostCount + 1));
    }

    public async Task DecrementPostCountAsync(DiscussionId discussionId)
    {
        var discussion = await dbContext.Discussions
            .Where(d => d.PublicId == discussionId.Value)
            .Select(d => new {
                d.Id,
                d.SpaceId,
                d.Space.HubId,
                d.Space.Hub.CommunityId })
            .FirstOrDefaultAsync();

        if (discussion is null) return;

        await dbContext.Discussions
            .Where(d => d.Id == discussion.Id)
            .ExecuteUpdateAsync(d => d.SetProperty(
                x => x.PostCount,
                x => x.PostCount - 1));

        await dbContext.Spaces
            .Where(s => s.Id == discussion.SpaceId)
            .ExecuteUpdateAsync(s => s.SetProperty(
                x => x.PostCount,
                x => x.PostCount - 1));

        await dbContext.Hubs
            .Where(h => h.Id == discussion.HubId)
            .ExecuteUpdateAsync(h => h.SetProperty(
                x => x.PostCount,
                x => x.PostCount - 1));

        await dbContext.Communities
            .Where(c => c.Id == discussion.CommunityId)
            .ExecuteUpdateAsync(c => c.SetProperty(
                x => x.PostCount,
                x => x.PostCount - 1));
    }

    public async Task IncrementReactionCountAsync(PostId postId, DiscussionId discussionId)
    {
        var discussion = await dbContext.Discussions
            .Where(d => d.PublicId == discussionId.Value)
            .Select(d => new {
                d.Id,
                d.SpaceId,
                d.Space.HubId,
                d.Space.Hub.CommunityId })
            .FirstOrDefaultAsync();

        if (discussion is null) return;

        await dbContext.Posts
            .Where(p => p.PublicId == postId.Value)
            .ExecuteUpdateAsync(p => p.SetProperty(
                x => x.ReactionCount,
                x => x.ReactionCount + 1));

        await dbContext.Discussions
            .Where(d => d.Id == discussion.Id)
            .ExecuteUpdateAsync(d => d.SetProperty(
                x => x.ReactionCount,
                x => x.ReactionCount + 1));

        await dbContext.Spaces
            .Where(s => s.Id == discussion.SpaceId)
            .ExecuteUpdateAsync(s => s.SetProperty(
                x => x.ReactionCount,
                x => x.ReactionCount + 1));

        await dbContext.Hubs
            .Where(h => h.Id == discussion.HubId)
            .ExecuteUpdateAsync(h => h.SetProperty(
                x => x.ReactionCount,
                x => x.ReactionCount + 1));

        await dbContext.Communities
            .Where(c => c.Id == discussion.CommunityId)
            .ExecuteUpdateAsync(c => c.SetProperty(
                x => x.ReactionCount,
                x => x.ReactionCount + 1));
    }

    public async Task DecrementReactionCountAsync(PostId postId, DiscussionId discussionId)
    {
        var discussion = await dbContext.Discussions
            .Where(d => d.PublicId == discussionId.Value)
            .Select(d => new {
                d.Id,
                d.SpaceId,
                d.Space.HubId,
                d.Space.Hub.CommunityId })
            .FirstOrDefaultAsync();

        if (discussion is null) return;

        await dbContext.Posts
            .Where(p => p.PublicId == postId.Value)
            .ExecuteUpdateAsync(p => p.SetProperty(
                x => x.ReactionCount,
                x => x.ReactionCount - 1));

        await dbContext.Discussions
            .Where(d => d.Id == discussion.Id)
            .ExecuteUpdateAsync(d => d.SetProperty(
                x => x.ReactionCount,
                x => x.ReactionCount - 1));

        await dbContext.Spaces
            .Where(s => s.Id == discussion.SpaceId)
            .ExecuteUpdateAsync(s => s.SetProperty(
                x => x.ReactionCount,
                x => x.ReactionCount - 1));

        await dbContext.Hubs
            .Where(h => h.Id == discussion.HubId)
            .ExecuteUpdateAsync(h => h.SetProperty(
                x => x.ReactionCount,
                x => x.ReactionCount - 1));

        await dbContext.Communities
            .Where(c => c.Id == discussion.CommunityId)
            .ExecuteUpdateAsync(c => c.SetProperty(
                x => x.ReactionCount,
                x => x.ReactionCount - 1));
    }

    // --- User-level counters ---

    public async Task IncrementUserDiscussionCountAsync(UserId userId) =>
        await dbContext.Users
            .Where(u => u.PublicId == userId.Value)
            .ExecuteUpdateAsync(u => u.SetProperty(
                x => x.DiscussionCount,
                x => x.DiscussionCount + 1));

    public async Task DecrementUserDiscussionCountAsync(UserId userId) =>
        await dbContext.Users
            .Where(u => u.PublicId == userId.Value)
            .ExecuteUpdateAsync(u => u.SetProperty(
                x => x.DiscussionCount,
                x => x.DiscussionCount - 1));

    public async Task IncrementUserReplyCountAsync(UserId userId) =>
        await dbContext.Users
            .Where(u => u.PublicId == userId.Value)
            .ExecuteUpdateAsync(u => u.SetProperty(
                x => x.ReplyCount,
                x => x.ReplyCount + 1));

    public async Task DecrementUserReplyCountAsync(UserId userId) =>
        await dbContext.Users
            .Where(u => u.PublicId == userId.Value)
            .ExecuteUpdateAsync(u => u.SetProperty(
                x => x.ReplyCount,
                x => x.ReplyCount - 1));

    public async Task IncrementUserFollowerCountAsync(UserId userId) =>
        await dbContext.Users
            .Where(u => u.PublicId == userId.Value)
            .ExecuteUpdateAsync(u => u.SetProperty(
                x => x.FollowerCount,
                x => x.FollowerCount + 1));

    public async Task DecrementUserFollowerCountAsync(UserId userId) =>
        await dbContext.Users
            .Where(u => u.PublicId == userId.Value)
            .ExecuteUpdateAsync(u => u.SetProperty(
                x => x.FollowerCount,
                x => x.FollowerCount - 1));

    // --- Space-level counters ---

    public async Task IncrementSpaceFollowerCountAsync(SpaceId spaceId) =>
        await dbContext.Spaces
            .Where(s => s.PublicId == spaceId.Value)
            .ExecuteUpdateAsync(s => s.SetProperty(
                x => x.FollowerCount,
                x => x.FollowerCount + 1));

    public async Task DecrementSpaceFollowerCountAsync(SpaceId spaceId) =>
        await dbContext.Spaces
            .Where(s => s.PublicId == spaceId.Value)
            .ExecuteUpdateAsync(s => s.SetProperty(
                x => x.FollowerCount,
                x => x.FollowerCount - 1));

    // --- Discussion-level counters ---

    public async Task IncrementDiscussionFollowerCountAsync(DiscussionId discussionId) =>
        await dbContext.Discussions
            .Where(d => d.PublicId == discussionId.Value)
            .ExecuteUpdateAsync(d => d.SetProperty(
                x => x.FollowerCount,
                x => x.FollowerCount + 1));

    public async Task DecrementDiscussionFollowerCountAsync(DiscussionId discussionId) =>
        await dbContext.Discussions
            .Where(d => d.PublicId == discussionId.Value)
            .ExecuteUpdateAsync(d => d.SetProperty(
                x => x.FollowerCount,
                x => x.FollowerCount - 1));

    // --- Notification counters ---

    public async Task IncrementUnreadNotificationCountAsync(UserId userId) =>
        await dbContext.Users
            .Where(u => u.PublicId == userId.Value)
            .ExecuteUpdateAsync(u => u.SetProperty(
                x => x.UnreadNotificationCount,
                x => x.UnreadNotificationCount + 1));

    public async Task DecrementUnreadNotificationCountAsync(UserId userId) =>
        await dbContext.Users
            .Where(u => u.PublicId == userId.Value)
            .ExecuteUpdateAsync(u => u.SetProperty(
                x => x.UnreadNotificationCount,
                x => x.UnreadNotificationCount - 1));

    public async Task ResetUnreadNotificationCountAsync(UserId userId)
    {
        try
        {
            await dbContext.Users
                .Where(u => u.PublicId == userId.Value)
                .ExecuteUpdateAsync(u => u.SetProperty(x => x.UnreadNotificationCount, 0));
        }
        catch (InvalidOperationException)
        {
            // Fallback for providers that don't support ExecuteUpdateAsync (e.g. InMemory)
            var user = await dbContext.Users.FirstOrDefaultAsync(u => u.PublicId == userId.Value);
            if (user is not null)
            {
                user.UnreadNotificationCount = 0;
                await dbContext.SaveChangesAsync();
            }
        }
    }
}
