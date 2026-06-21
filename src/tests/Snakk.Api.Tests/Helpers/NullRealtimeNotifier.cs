using Snakk.Application.Services;
using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;

namespace Snakk.Api.Tests.Helpers;

/// <summary>
/// No-op IRealtimeNotifier for integration tests — prevents outbound HTTP calls to
/// the Realtime service (localhost:15300) which is not running during test execution.
/// </summary>
internal sealed class NullRealtimeNotifier : IRealtimeNotifier
{
    public Task NotifyPostCreatedAsync(Post post, User author, Discussion discussion) => Task.CompletedTask;
    public Task NotifyPostEditedAsync(Post post, User author, Discussion discussion) => Task.CompletedTask;
    public Task NotifyPostDeletedAsync(PostId postId, DiscussionId discussionId, bool isHardDelete) => Task.CompletedTask;
    public Task NotifyUserAsync(UserId userId, object notification) => Task.CompletedTask;
    public Task NotifyReactionUpdatedAsync(PostId postId, DiscussionId discussionId, Dictionary<ReactionType, int> counts) => Task.CompletedTask;
    public Task NotifyUnreadCountUpdatedAsync(UserId userId, int count) => Task.CompletedTask;
    public Task NotifyDiscussionLockedAsync(DiscussionId discussionId) => Task.CompletedTask;
    public Task NotifyDiscussionUnlockedAsync(DiscussionId discussionId) => Task.CompletedTask;
    public Task NotifyDiscussionCreatedAsync(DiscussionId discussionId, SpaceId spaceId, HubId hubId) => Task.CompletedTask;
    public Task NotifyGlobalAsync(string eventType, string message) => Task.CompletedTask;
    public Task NotifyDiscussionPinnedAsync(DiscussionId discussionId, SpaceId spaceId, bool isPinned) => Task.CompletedTask;
    public Task NotifyDiscussionDeletedAsync(DiscussionId discussionId, SpaceId spaceId) => Task.CompletedTask;
    public Task NotifyDiscussionTitleUpdatedAsync(DiscussionId discussionId, string newTitle) => Task.CompletedTask;
    public Task NotifyBannerUpdatedAsync(string scopeType, string scopePublicId) => Task.CompletedTask;
    public Task NotifyReadStateUpdatedAsync(UserId userId, string discussionId, string postId) => Task.CompletedTask;
    public Task NotifyPostCountUpdatedAsync(DiscussionId discussionId, SpaceId spaceId, HubId? hubId, int delta,
        string? lastPostExcerpt = null, string? lastReplierId = null, string? lastReplierName = null,
        string? lastReplierAvatarUrl = null, long? lastActivityAtUnix = null) => Task.CompletedTask;
    public Task NotifyDiscussionReactionCountAsync(DiscussionId discussionId, SpaceId spaceId, int delta) => Task.CompletedTask;
    public Task NotifyDebateUpdatedAsync(DiscussionId discussionId, IReadOnlyList<DebatePositionUpdate> positions) => Task.CompletedTask;
    public Task NotifyPollUpdatedAsync(DiscussionId discussionId, IReadOnlyList<PollOptionUpdate> options, int totalVotes) => Task.CompletedTask;
    public Task NotifyDirectMessageCountUpdatedAsync(string userPublicId, int userIntId, int unreadCount) => Task.CompletedTask;
    public Task NotifyDmMessagesDeletedAsync(string recipientPublicId, int recipientIntId, string conversationId, IReadOnlyList<string> messageIds) => Task.CompletedTask;
}
