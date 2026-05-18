namespace Snakk.Application.Services;

using Snakk.Domain.ValueObjects;

/// <summary>
/// Service to update denormalized counts across the hierarchy.
/// All methods are idempotent and use atomic increments/decrements.
/// </summary>
public interface ICounterService
{
    /// <summary>
    /// Increment counts when a new discussion is created.
    /// Increments: Space.DiscussionCount, Hub.DiscussionCount, Community.DiscussionCount
    /// </summary>
    Task IncrementDiscussionCountAsync(SpaceId spaceId, CancellationToken ct = default);

    /// <summary>
    /// Decrement counts when a discussion is deleted.
    /// Decrements: Space.DiscussionCount, Hub.DiscussionCount, Community.DiscussionCount
    /// </summary>
    Task DecrementDiscussionCountAsync(SpaceId spaceId, CancellationToken ct = default);

    /// <summary>
    /// Increment counts when a new post is created.
    /// Increments: Discussion.PostCount, Space.PostCount, Hub.PostCount, Community.PostCount
    /// </summary>
    Task IncrementPostCountAsync(DiscussionId discussionId, CancellationToken ct = default);

    /// <summary>
    /// Decrement counts when a post is deleted.
    /// Decrements: Discussion.PostCount, Space.PostCount, Hub.PostCount, Community.PostCount
    /// </summary>
    Task DecrementPostCountAsync(DiscussionId discussionId, CancellationToken ct = default);

    /// <summary>
    /// Increment reaction counts across the hierarchy.
    /// Increments: Post.ReactionCount, Discussion.ReactionCount, Space.ReactionCount, Hub.ReactionCount, Community.ReactionCount
    /// </summary>
    Task IncrementReactionCountAsync(PostId postId, DiscussionId discussionId, CancellationToken ct = default);

    /// <summary>
    /// Decrement reaction counts across the hierarchy.
    /// Decrements: Post.ReactionCount, Discussion.ReactionCount, Space.ReactionCount, Hub.ReactionCount, Community.ReactionCount
    /// </summary>
    Task DecrementReactionCountAsync(PostId postId, DiscussionId discussionId, CancellationToken ct = default);

    // --- User-level counters ---

    /// <summary>
    /// Increment User.DiscussionCount when a user creates a discussion.
    /// </summary>
    Task IncrementUserDiscussionCountAsync(UserId userId, CancellationToken ct = default);

    /// <summary>
    /// Decrement User.DiscussionCount when a user's discussion is deleted.
    /// </summary>
    Task DecrementUserDiscussionCountAsync(UserId userId, CancellationToken ct = default);

    /// <summary>
    /// Increment User.ReplyCount when a user creates a reply (non-first post).
    /// </summary>
    Task IncrementUserReplyCountAsync(UserId userId, CancellationToken ct = default);

    /// <summary>
    /// Decrement User.ReplyCount when a user's reply is deleted.
    /// </summary>
    Task DecrementUserReplyCountAsync(UserId userId, CancellationToken ct = default);

    /// <summary>
    /// Increment User.FollowerCount when someone follows a user.
    /// </summary>
    Task IncrementUserFollowerCountAsync(UserId userId, CancellationToken ct = default);

    /// <summary>
    /// Decrement User.FollowerCount when someone unfollows a user.
    /// </summary>
    Task DecrementUserFollowerCountAsync(UserId userId, CancellationToken ct = default);

    // --- Space-level counters ---

    /// <summary>
    /// Increment Space.FollowerCount when someone follows a space.
    /// </summary>
    Task IncrementSpaceFollowerCountAsync(SpaceId spaceId, CancellationToken ct = default);

    /// <summary>
    /// Decrement Space.FollowerCount when someone unfollows a space.
    /// </summary>
    Task DecrementSpaceFollowerCountAsync(SpaceId spaceId, CancellationToken ct = default);

    // --- Discussion-level counters ---

    /// <summary>
    /// Increment Discussion.FollowerCount when someone follows a discussion.
    /// </summary>
    Task IncrementDiscussionFollowerCountAsync(DiscussionId discussionId, CancellationToken ct = default);

    /// <summary>
    /// Decrement Discussion.FollowerCount when someone unfollows a discussion.
    /// </summary>
    Task DecrementDiscussionFollowerCountAsync(DiscussionId discussionId, CancellationToken ct = default);

    // --- Notification counters ---

    /// <summary>
    /// Increment User.UnreadNotificationCount when a new notification is created.
    /// </summary>
    Task IncrementUnreadNotificationCountAsync(UserId userId, CancellationToken ct = default);

    /// <summary>
    /// Decrement User.UnreadNotificationCount when a notification is marked as read.
    /// </summary>
    Task DecrementUnreadNotificationCountAsync(UserId userId, CancellationToken ct = default);

    /// <summary>
    /// Reset User.UnreadNotificationCount to 0 when all notifications are marked as read.
    /// </summary>
    Task ResetUnreadNotificationCountAsync(UserId userId, CancellationToken ct = default);
}
