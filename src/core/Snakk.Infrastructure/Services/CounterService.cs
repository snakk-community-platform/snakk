namespace Snakk.Infrastructure.Services;

using Microsoft.EntityFrameworkCore;
using Snakk.Application.Services;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Database;

public class CounterService(IDbContextFactory<SnakkDbContext> dbFactory) : ICounterService
{
    // Each update gets its own context so parallel calls are thread-safe.
    private async Task RunUpdateAsync(Func<SnakkDbContext, Task> update)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        await update(db);
    }

    public async Task IncrementDiscussionCountAsync(SpaceId spaceId)
    {
        int spaceDbId, hubId, communityId;
        {
            await using var db = await dbFactory.CreateDbContextAsync();
            var space = await db.Spaces
                .Where(s => s.PublicId == spaceId.Value)
                .Select(s => new { s.Id, s.HubId, s.Hub.CommunityId })
                .FirstOrDefaultAsync();
            if (space is null) return;
            spaceDbId = space.Id; hubId = space.HubId; communityId = space.CommunityId;
        }

        await Task.WhenAll(
            RunUpdateAsync(db => db.Spaces.Where(s => s.Id == spaceDbId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.DiscussionCount, x => x.DiscussionCount + 1))),
            RunUpdateAsync(db => db.Hubs.Where(h => h.Id == hubId)
                .ExecuteUpdateAsync(h => h.SetProperty(x => x.DiscussionCount, x => x.DiscussionCount + 1))),
            RunUpdateAsync(db => db.Communities.Where(c => c.Id == communityId)
                .ExecuteUpdateAsync(c => c.SetProperty(x => x.DiscussionCount, x => x.DiscussionCount + 1)))
        );
    }

    public async Task DecrementDiscussionCountAsync(SpaceId spaceId)
    {
        int spaceDbId, hubId, communityId;
        {
            await using var db = await dbFactory.CreateDbContextAsync();
            var space = await db.Spaces
                .Where(s => s.PublicId == spaceId.Value)
                .Select(s => new { s.Id, s.HubId, s.Hub.CommunityId })
                .FirstOrDefaultAsync();
            if (space is null) return;
            spaceDbId = space.Id; hubId = space.HubId; communityId = space.CommunityId;
        }

        await Task.WhenAll(
            RunUpdateAsync(db => db.Spaces.Where(s => s.Id == spaceDbId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.DiscussionCount, x => x.DiscussionCount - 1))),
            RunUpdateAsync(db => db.Hubs.Where(h => h.Id == hubId)
                .ExecuteUpdateAsync(h => h.SetProperty(x => x.DiscussionCount, x => x.DiscussionCount - 1))),
            RunUpdateAsync(db => db.Communities.Where(c => c.Id == communityId)
                .ExecuteUpdateAsync(c => c.SetProperty(x => x.DiscussionCount, x => x.DiscussionCount - 1)))
        );
    }

    public async Task IncrementPostCountAsync(DiscussionId discussionId)
    {
        int discussionDbId, spaceId, hubId, communityId;
        {
            await using var db = await dbFactory.CreateDbContextAsync();
            var discussion = await db.Discussions
                .Where(d => d.PublicId == discussionId.Value)
                .Select(d => new { d.Id, d.SpaceId, d.HubId, d.CommunityId })
                .FirstOrDefaultAsync();
            if (discussion is null) return;
            discussionDbId = discussion.Id; spaceId = discussion.SpaceId;
            hubId = discussion.HubId; communityId = discussion.CommunityId;
        }

        await Task.WhenAll(
            RunUpdateAsync(db => db.Discussions.Where(d => d.Id == discussionDbId)
                .ExecuteUpdateAsync(d => d.SetProperty(x => x.PostCount, x => x.PostCount + 1))),
            RunUpdateAsync(db => db.Spaces.Where(s => s.Id == spaceId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.PostCount, x => x.PostCount + 1))),
            RunUpdateAsync(db => db.Hubs.Where(h => h.Id == hubId)
                .ExecuteUpdateAsync(h => h.SetProperty(x => x.PostCount, x => x.PostCount + 1))),
            RunUpdateAsync(db => db.Communities.Where(c => c.Id == communityId)
                .ExecuteUpdateAsync(c => c.SetProperty(x => x.PostCount, x => x.PostCount + 1)))
        );
    }

    public async Task DecrementPostCountAsync(DiscussionId discussionId)
    {
        int discussionDbId, spaceId, hubId, communityId;
        {
            await using var db = await dbFactory.CreateDbContextAsync();
            var discussion = await db.Discussions
                .Where(d => d.PublicId == discussionId.Value)
                .Select(d => new { d.Id, d.SpaceId, d.HubId, d.CommunityId })
                .FirstOrDefaultAsync();
            if (discussion is null) return;
            discussionDbId = discussion.Id; spaceId = discussion.SpaceId;
            hubId = discussion.HubId; communityId = discussion.CommunityId;
        }

        await Task.WhenAll(
            RunUpdateAsync(db => db.Discussions.Where(d => d.Id == discussionDbId)
                .ExecuteUpdateAsync(d => d.SetProperty(x => x.PostCount, x => x.PostCount - 1))),
            RunUpdateAsync(db => db.Spaces.Where(s => s.Id == spaceId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.PostCount, x => x.PostCount - 1))),
            RunUpdateAsync(db => db.Hubs.Where(h => h.Id == hubId)
                .ExecuteUpdateAsync(h => h.SetProperty(x => x.PostCount, x => x.PostCount - 1))),
            RunUpdateAsync(db => db.Communities.Where(c => c.Id == communityId)
                .ExecuteUpdateAsync(c => c.SetProperty(x => x.PostCount, x => x.PostCount - 1)))
        );
    }

    public async Task IncrementReactionCountAsync(PostId postId, DiscussionId discussionId)
    {
        int discussionDbId, spaceId, hubId, communityId;
        {
            await using var db = await dbFactory.CreateDbContextAsync();
            var discussion = await db.Discussions
                .Where(d => d.PublicId == discussionId.Value)
                .Select(d => new { d.Id, d.SpaceId, d.HubId, d.CommunityId })
                .FirstOrDefaultAsync();
            if (discussion is null) return;
            discussionDbId = discussion.Id; spaceId = discussion.SpaceId;
            hubId = discussion.HubId; communityId = discussion.CommunityId;
        }

        var postPublicId = postId.Value;
        await Task.WhenAll(
            RunUpdateAsync(db => db.Posts.Where(p => p.PublicId == postPublicId)
                .ExecuteUpdateAsync(p => p.SetProperty(x => x.ReactionCount, x => x.ReactionCount + 1))),
            RunUpdateAsync(db => db.Discussions.Where(d => d.Id == discussionDbId)
                .ExecuteUpdateAsync(d => d.SetProperty(x => x.ReactionCount, x => x.ReactionCount + 1))),
            RunUpdateAsync(db => db.Spaces.Where(s => s.Id == spaceId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.ReactionCount, x => x.ReactionCount + 1))),
            RunUpdateAsync(db => db.Hubs.Where(h => h.Id == hubId)
                .ExecuteUpdateAsync(h => h.SetProperty(x => x.ReactionCount, x => x.ReactionCount + 1))),
            RunUpdateAsync(db => db.Communities.Where(c => c.Id == communityId)
                .ExecuteUpdateAsync(c => c.SetProperty(x => x.ReactionCount, x => x.ReactionCount + 1)))
        );
    }

    public async Task DecrementReactionCountAsync(PostId postId, DiscussionId discussionId)
    {
        int discussionDbId, spaceId, hubId, communityId;
        {
            await using var db = await dbFactory.CreateDbContextAsync();
            var discussion = await db.Discussions
                .Where(d => d.PublicId == discussionId.Value)
                .Select(d => new { d.Id, d.SpaceId, d.HubId, d.CommunityId })
                .FirstOrDefaultAsync();
            if (discussion is null) return;
            discussionDbId = discussion.Id; spaceId = discussion.SpaceId;
            hubId = discussion.HubId; communityId = discussion.CommunityId;
        }

        var postPublicId = postId.Value;
        await Task.WhenAll(
            RunUpdateAsync(db => db.Posts.Where(p => p.PublicId == postPublicId)
                .ExecuteUpdateAsync(p => p.SetProperty(x => x.ReactionCount, x => x.ReactionCount - 1))),
            RunUpdateAsync(db => db.Discussions.Where(d => d.Id == discussionDbId)
                .ExecuteUpdateAsync(d => d.SetProperty(x => x.ReactionCount, x => x.ReactionCount - 1))),
            RunUpdateAsync(db => db.Spaces.Where(s => s.Id == spaceId)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.ReactionCount, x => x.ReactionCount - 1))),
            RunUpdateAsync(db => db.Hubs.Where(h => h.Id == hubId)
                .ExecuteUpdateAsync(h => h.SetProperty(x => x.ReactionCount, x => x.ReactionCount - 1))),
            RunUpdateAsync(db => db.Communities.Where(c => c.Id == communityId)
                .ExecuteUpdateAsync(c => c.SetProperty(x => x.ReactionCount, x => x.ReactionCount - 1)))
        );
    }

    // --- User-level counters (single update — no parallelism needed) ---

    public Task IncrementUserDiscussionCountAsync(UserId userId) =>
        RunUpdateAsync(db => db.Users.Where(u => u.PublicId == userId.Value)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.DiscussionCount, x => x.DiscussionCount + 1)));

    public Task DecrementUserDiscussionCountAsync(UserId userId) =>
        RunUpdateAsync(db => db.Users.Where(u => u.PublicId == userId.Value)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.DiscussionCount, x => x.DiscussionCount - 1)));

    public Task IncrementUserReplyCountAsync(UserId userId) =>
        RunUpdateAsync(db => db.Users.Where(u => u.PublicId == userId.Value)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.ReplyCount, x => x.ReplyCount + 1)));

    public Task DecrementUserReplyCountAsync(UserId userId) =>
        RunUpdateAsync(db => db.Users.Where(u => u.PublicId == userId.Value)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.ReplyCount, x => x.ReplyCount - 1)));

    public Task IncrementUserFollowerCountAsync(UserId userId) =>
        RunUpdateAsync(db => db.Users.Where(u => u.PublicId == userId.Value)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.FollowerCount, x => x.FollowerCount + 1)));

    public Task DecrementUserFollowerCountAsync(UserId userId) =>
        RunUpdateAsync(db => db.Users.Where(u => u.PublicId == userId.Value)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.FollowerCount, x => x.FollowerCount - 1)));

    // --- Space-level counters ---

    public Task IncrementSpaceFollowerCountAsync(SpaceId spaceId) =>
        RunUpdateAsync(db => db.Spaces.Where(s => s.PublicId == spaceId.Value)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.FollowerCount, x => x.FollowerCount + 1)));

    public Task DecrementSpaceFollowerCountAsync(SpaceId spaceId) =>
        RunUpdateAsync(db => db.Spaces.Where(s => s.PublicId == spaceId.Value)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.FollowerCount, x => x.FollowerCount - 1)));

    // --- Discussion-level counters ---

    public Task IncrementDiscussionFollowerCountAsync(DiscussionId discussionId) =>
        RunUpdateAsync(db => db.Discussions.Where(d => d.PublicId == discussionId.Value)
            .ExecuteUpdateAsync(d => d.SetProperty(x => x.FollowerCount, x => x.FollowerCount + 1)));

    public Task DecrementDiscussionFollowerCountAsync(DiscussionId discussionId) =>
        RunUpdateAsync(db => db.Discussions.Where(d => d.PublicId == discussionId.Value)
            .ExecuteUpdateAsync(d => d.SetProperty(x => x.FollowerCount, x => x.FollowerCount - 1)));

    // --- Notification counters ---

    public Task IncrementUnreadNotificationCountAsync(UserId userId) =>
        RunUpdateAsync(db => db.Users.Where(u => u.PublicId == userId.Value)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.UnreadNotificationCount, x => x.UnreadNotificationCount + 1)));

    public Task DecrementUnreadNotificationCountAsync(UserId userId) =>
        RunUpdateAsync(db => db.Users.Where(u => u.PublicId == userId.Value)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.UnreadNotificationCount, x => x.UnreadNotificationCount - 1)));

    public async Task ResetUnreadNotificationCountAsync(UserId userId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        try
        {
            await db.Users
                .Where(u => u.PublicId == userId.Value)
                .ExecuteUpdateAsync(u => u.SetProperty(x => x.UnreadNotificationCount, 0));
        }
        catch (InvalidOperationException)
        {
            // Fallback for providers that don't support ExecuteUpdateAsync (e.g. InMemory)
            var user = await db.Users.FirstOrDefaultAsync(u => u.PublicId == userId.Value);
            if (user is not null)
            {
                user.UnreadNotificationCount = 0;
                await db.SaveChangesAsync();
            }
        }
    }
}
