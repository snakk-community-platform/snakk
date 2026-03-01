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
    Task IncrementDiscussionCountAsync(SpaceId spaceId);

    /// <summary>
    /// Decrement counts when a discussion is deleted.
    /// Decrements: Space.DiscussionCount, Hub.DiscussionCount, Community.DiscussionCount
    /// </summary>
    Task DecrementDiscussionCountAsync(SpaceId spaceId);

    /// <summary>
    /// Increment counts when a new post is created.
    /// Increments: Discussion.PostCount, Space.PostCount, Hub.PostCount, Community.PostCount
    /// </summary>
    Task IncrementPostCountAsync(DiscussionId discussionId);

    /// <summary>
    /// Decrement counts when a post is deleted.
    /// Decrements: Discussion.PostCount, Space.PostCount, Hub.PostCount, Community.PostCount
    /// </summary>
    Task DecrementPostCountAsync(DiscussionId discussionId);

    /// <summary>
    /// Increment unique reactor count when a user reacts to a discussion for the first time.
    /// Only increments if this is the user's first reaction to any post in the discussion.
    /// Increments: Discussion.ReactionCount
    /// </summary>
    Task IncrementUniqueReactorCountAsync(DiscussionId discussionId, UserId userId);

    /// <summary>
    /// Decrement unique reactor count when a user removes their last reaction from a discussion.
    /// Only decrements if the user has no more reactions to any post in the discussion.
    /// Decrements: Discussion.ReactionCount
    /// </summary>
    Task DecrementUniqueReactorCountAsync(DiscussionId discussionId, UserId userId);

    // --- User-level counters ---

    /// <summary>
    /// Increment User.DiscussionCount when a user creates a discussion.
    /// </summary>
    Task IncrementUserDiscussionCountAsync(UserId userId);

    /// <summary>
    /// Decrement User.DiscussionCount when a user's discussion is deleted.
    /// </summary>
    Task DecrementUserDiscussionCountAsync(UserId userId);

    /// <summary>
    /// Increment User.ReplyCount when a user creates a reply (non-first post).
    /// </summary>
    Task IncrementUserReplyCountAsync(UserId userId);

    /// <summary>
    /// Decrement User.ReplyCount when a user's reply is deleted.
    /// </summary>
    Task DecrementUserReplyCountAsync(UserId userId);

    /// <summary>
    /// Increment User.FollowerCount when someone follows a user.
    /// </summary>
    Task IncrementUserFollowerCountAsync(UserId userId);

    /// <summary>
    /// Decrement User.FollowerCount when someone unfollows a user.
    /// </summary>
    Task DecrementUserFollowerCountAsync(UserId userId);

    // --- Discussion-level counters ---

    /// <summary>
    /// Increment Discussion.FollowerCount when someone follows a discussion.
    /// </summary>
    Task IncrementDiscussionFollowerCountAsync(DiscussionId discussionId);

    /// <summary>
    /// Decrement Discussion.FollowerCount when someone unfollows a discussion.
    /// </summary>
    Task DecrementDiscussionFollowerCountAsync(DiscussionId discussionId);

    // --- Notification counters ---

    /// <summary>
    /// Increment User.UnreadNotificationCount when a new notification is created.
    /// </summary>
    Task IncrementUnreadNotificationCountAsync(UserId userId);

    /// <summary>
    /// Decrement User.UnreadNotificationCount when a notification is marked as read.
    /// </summary>
    Task DecrementUnreadNotificationCountAsync(UserId userId);

    /// <summary>
    /// Reset User.UnreadNotificationCount to 0 when all notifications are marked as read.
    /// </summary>
    Task ResetUnreadNotificationCountAsync(UserId userId);
}
