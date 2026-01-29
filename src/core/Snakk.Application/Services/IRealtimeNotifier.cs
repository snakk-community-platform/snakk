namespace Snakk.Application.Services;

using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;

/// <summary>
/// Application-layer abstraction for sending realtime notifications.
/// Implementations handle presentation concerns (HTML, JSON, etc.)
/// </summary>
public interface IRealtimeNotifier
{
    /// <summary>
    /// Notify subscribers when a new post is created
    /// </summary>
    Task NotifyPostCreatedAsync(Post post, User author, Discussion discussion);

    /// <summary>
    /// Notify subscribers when a post is edited
    /// </summary>
    Task NotifyPostEditedAsync(Post post, User author, Discussion discussion);

    /// <summary>
    /// Notify subscribers when a post is deleted
    /// </summary>
    Task NotifyPostDeletedAsync(PostId postId, DiscussionId discussionId, bool isHardDelete);

    /// <summary>
    /// Notify a specific user (for notifications)
    /// </summary>
    Task NotifyUserAsync(UserId userId, object notification);

    /// <summary>
    /// Notify subscribers when reaction counts change on a post
    /// </summary>
    Task NotifyReactionUpdatedAsync(PostId postId, DiscussionId discussionId, Dictionary<Domain.ValueObjects.ReactionType, int> counts);

    /// <summary>
    /// Notify user when their unread notification count changes
    /// </summary>
    Task NotifyUnreadCountUpdatedAsync(UserId userId, int count);
}
