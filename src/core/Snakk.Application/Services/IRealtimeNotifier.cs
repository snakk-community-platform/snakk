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

    /// <summary>
    /// Notify subscribers when a discussion is locked
    /// </summary>
    Task NotifyDiscussionLockedAsync(DiscussionId discussionId);

    /// <summary>
    /// Notify subscribers when a discussion is unlocked
    /// </summary>
    Task NotifyDiscussionUnlockedAsync(DiscussionId discussionId);

    /// <summary>
    /// Notify space, hub, and global subscribers when a new discussion is created
    /// </summary>
    Task NotifyDiscussionCreatedAsync(DiscussionId discussionId, SpaceId spaceId, HubId hubId);

    /// <summary>
    /// Broadcast a message to all connected clients (global announcements, maintenance alerts)
    /// </summary>
    Task NotifyGlobalAsync(string eventType, string message);

    /// <summary>
    /// Notify space subscribers when a discussion is pinned or unpinned
    /// </summary>
    Task NotifyDiscussionPinnedAsync(DiscussionId discussionId, SpaceId spaceId, bool isPinned);

    /// <summary>
    /// Notify space subscribers when a discussion is deleted
    /// </summary>
    Task NotifyDiscussionDeletedAsync(DiscussionId discussionId, SpaceId spaceId);

    /// <summary>
    /// Notify discussion subscribers when the discussion title changes
    /// </summary>
    Task NotifyDiscussionTitleUpdatedAsync(DiscussionId discussionId, string newTitle);

    /// <summary>
    /// Notify scope subscribers when an banner is published, updated, or deleted
    /// </summary>
    Task NotifyBannerUpdatedAsync(string scopeType, string scopePublicId);

    /// <summary>
    /// Notify a user's other tabs when a discussion read state is updated
    /// </summary>
    Task NotifyReadStateUpdatedAsync(UserId userId, string discussionId, string postId);

    /// <summary>
    /// Notify space and hub subscribers when a post count changes for a discussion.
    /// Optional reply strip fields update the last-reply preview on feed cards (null on decrements).
    /// </summary>
    Task NotifyPostCountUpdatedAsync(
        DiscussionId discussionId, SpaceId spaceId, HubId? hubId, int delta,
        string? lastPostExcerpt = null,
        string? lastReplierId = null,
        string? lastReplierName = null,
        string? lastReplierAvatarUrl = null,
        long? lastActivityAtUnix = null);

    /// <summary>
    /// Notify space subscribers when the total reaction count on a discussion changes
    /// </summary>
    Task NotifyDiscussionReactionCountAsync(DiscussionId discussionId, SpaceId spaceId, int delta);

    /// <summary>
    /// Notify global subscribers when debate position vote counts change
    /// </summary>
    Task NotifyDebateUpdatedAsync(DiscussionId discussionId, IReadOnlyList<DebatePositionUpdate> positions);

    /// <summary>
    /// Notify global subscribers when poll vote counts change
    /// </summary>
    Task NotifyPollUpdatedAsync(DiscussionId discussionId, IReadOnlyList<PollOptionUpdate> options, int totalVotes);

    /// <summary>
    /// Notify a user when their unread DM conversation count changes
    /// </summary>
    Task NotifyDirectMessageCountUpdatedAsync(string userPublicId, int userIntId, int unreadCount);

    /// <summary>
    /// Notify a user when specific DM messages are hard-deleted.
    /// Empty messageIds means the entire conversation was deleted.
    /// </summary>
    Task NotifyDmMessagesDeletedAsync(string recipientPublicId, int recipientIntId, string conversationId, IReadOnlyList<string> messageIds);
}

public record DebatePositionUpdate(int Index, string Label, int PostCount, int Pct);
public record PollOptionUpdate(string Text, int VoteCount, int Pct);
