namespace Snakk.Infrastructure.Services;

using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using Snakk.Application.Services;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Database;

public class CounterService(IDbContextFactory<SnakkDbContext> dbFactory, IConnectionMultiplexer redis) : ICounterService
{
    private const string PostCountKeyPrefix = "snakk:counter:post:discussion:";
    private const string FollowerSpaceKeyPrefix = "snakk:counter:follower:space:";
    private const string FollowerDiscussionKeyPrefix = "snakk:counter:follower:discussion:";
    private const string FollowerUserKeyPrefix = "snakk:counter:follower:user:";
    private const string UserDiscussionsKeyPrefix = "snakk:counter:user-discussions:";
    private const string UserRepliesKeyPrefix = "snakk:counter:user-replies:";
    private const string DiscussionSpaceKeyPrefix = "snakk:counter:discussions:space:";
    private const string ReactionPostKeyPrefix = "snakk:counter:reaction:post:";

    // Each update gets its own context so parallel calls are thread-safe.
    private async Task RunUpdateAsync(Func<SnakkDbContext, Task> update)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        await update(db);
    }

    public Task IncrementDiscussionCountAsync(SpaceId spaceId, CancellationToken ct = default) =>
        redis.GetDatabase().StringIncrementAsync(DiscussionSpaceKeyPrefix + spaceId.Value);

    public Task DecrementDiscussionCountAsync(SpaceId spaceId, CancellationToken ct = default) =>
        redis.GetDatabase().StringDecrementAsync(DiscussionSpaceKeyPrefix + spaceId.Value);

    public Task IncrementPostCountAsync(DiscussionId discussionId, CancellationToken ct = default)
    {
        var db = redis.GetDatabase();
        return db.StringIncrementAsync(PostCountKeyPrefix + discussionId.Value);
    }

    public Task DecrementPostCountAsync(DiscussionId discussionId, CancellationToken ct = default)
    {
        var db = redis.GetDatabase();
        return db.StringDecrementAsync(PostCountKeyPrefix + discussionId.Value);
    }

    public Task IncrementReactionCountAsync(PostId postId, DiscussionId discussionId, CancellationToken ct = default) =>
        redis.GetDatabase().StringIncrementAsync(ReactionPostKeyPrefix + postId.Value);

    public Task DecrementReactionCountAsync(PostId postId, DiscussionId discussionId, CancellationToken ct = default) =>
        redis.GetDatabase().StringDecrementAsync(ReactionPostKeyPrefix + postId.Value);

    // --- User-level counters (buffered in Valkey, flushed hourly) ---

    public Task IncrementUserDiscussionCountAsync(UserId userId, CancellationToken ct = default) =>
        redis.GetDatabase().StringIncrementAsync(UserDiscussionsKeyPrefix + userId.Value);

    public Task DecrementUserDiscussionCountAsync(UserId userId, CancellationToken ct = default) =>
        redis.GetDatabase().StringDecrementAsync(UserDiscussionsKeyPrefix + userId.Value);

    public Task IncrementUserReplyCountAsync(UserId userId, CancellationToken ct = default) =>
        redis.GetDatabase().StringIncrementAsync(UserRepliesKeyPrefix + userId.Value);

    public Task DecrementUserReplyCountAsync(UserId userId, CancellationToken ct = default) =>
        redis.GetDatabase().StringDecrementAsync(UserRepliesKeyPrefix + userId.Value);

    public Task IncrementUserFollowerCountAsync(UserId userId, CancellationToken ct = default) =>
        redis.GetDatabase().StringIncrementAsync(FollowerUserKeyPrefix + userId.Value);

    public Task DecrementUserFollowerCountAsync(UserId userId, CancellationToken ct = default) =>
        redis.GetDatabase().StringDecrementAsync(FollowerUserKeyPrefix + userId.Value);

    // --- Space follower count (buffered in Valkey, flushed hourly) ---

    public Task IncrementSpaceFollowerCountAsync(SpaceId spaceId, CancellationToken ct = default) =>
        redis.GetDatabase().StringIncrementAsync(FollowerSpaceKeyPrefix + spaceId.Value);

    public Task DecrementSpaceFollowerCountAsync(SpaceId spaceId, CancellationToken ct = default) =>
        redis.GetDatabase().StringDecrementAsync(FollowerSpaceKeyPrefix + spaceId.Value);

    // --- Discussion follower count (buffered in Valkey, flushed hourly) ---

    public Task IncrementDiscussionFollowerCountAsync(DiscussionId discussionId, CancellationToken ct = default) =>
        redis.GetDatabase().StringIncrementAsync(FollowerDiscussionKeyPrefix + discussionId.Value);

    public Task DecrementDiscussionFollowerCountAsync(DiscussionId discussionId, CancellationToken ct = default) =>
        redis.GetDatabase().StringDecrementAsync(FollowerDiscussionKeyPrefix + discussionId.Value);

    // --- Notification counters ---

    public Task IncrementUnreadNotificationCountAsync(UserId userId, CancellationToken ct = default) =>
        RunUpdateAsync(db => db.Users.Where(u => u.PublicId == userId.Value)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.UnreadNotificationCount, x => x.UnreadNotificationCount + 1)));

    public Task DecrementUnreadNotificationCountAsync(UserId userId, CancellationToken ct = default) =>
        RunUpdateAsync(db => db.Users.Where(u => u.PublicId == userId.Value)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.UnreadNotificationCount, x => x.UnreadNotificationCount - 1)));

    public async Task ResetUnreadNotificationCountAsync(UserId userId, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        try
        {
            await db.Users
                .Where(u => u.PublicId == userId.Value)
                .ExecuteUpdateAsync(u => u.SetProperty(x => x.UnreadNotificationCount, 0), ct);
        }
        catch (InvalidOperationException)
        {
            // Fallback for providers that don't support ExecuteUpdateAsync (e.g. InMemory)
            var user = await db.Users.FirstOrDefaultAsync(u => u.PublicId == userId.Value, ct);
            if (user is not null)
            {
                user.UnreadNotificationCount = 0;
                await db.SaveChangesAsync(ct);
            }
        }
    }
}
