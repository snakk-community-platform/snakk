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
}
