namespace Snakk.Application.Repositories;

public interface ICounterRepository
{
    /// <summary>
    /// Reads and deletes all buffered post-count deltas from Valkey, then applies them to
    /// Discussion (PostCount + EngagementScore recompute), Space, Hub, and Community tables.
    /// </summary>
    Task FlushPostCountsAsync(CancellationToken ct = default);

    /// <summary>
    /// Reads and deletes all buffered follower-count deltas from Valkey, then applies them to
    /// Space.FollowerCount, Discussion.FollowerCount, and User.FollowerCount.
    /// </summary>
    Task FlushFollowerCountsAsync(CancellationToken ct = default);

    /// <summary>
    /// Reads and deletes all buffered user activity-count deltas from Valkey, then applies them to
    /// User.DiscussionCount and User.ReplyCount.
    /// </summary>
    Task FlushUserCountsAsync(CancellationToken ct = default);

    /// <summary>
    /// Reads and deletes all buffered discussion-count deltas from Valkey (keyed by space), then
    /// applies them to Space.DiscussionCount, Hub.DiscussionCount, and Community.DiscussionCount.
    /// </summary>
    Task FlushDiscussionCountsAsync(CancellationToken ct = default);

    /// <summary>
    /// Reads and deletes all buffered reaction-count deltas from Valkey (keyed by post), then
    /// applies them to Post.ReactionCount, Discussion.ReactionCount + EngagementScore,
    /// Space.ReactionCount, Hub.ReactionCount, and Community.ReactionCount.
    /// </summary>
    Task FlushReactionCountsAsync(CancellationToken ct = default);

    /// <summary>
    /// Pops all discussion IDs from the Valkey trending-dirty set and recalculates
    /// Discussion.TrendScore for each using the full time-decay formula.
    /// </summary>
    Task FlushTrendScoresAsync(CancellationToken ct = default);

    /// <summary>
    /// Pops all pending read-state entries from the Valkey dirty set, reads each value
    /// (last-write-wins), and batch-upserts them into the DiscussionReadState table.
    /// </summary>
    Task FlushReadStatesAsync(CancellationToken ct = default);
}
