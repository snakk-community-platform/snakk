namespace Snakk.Application.Services;

/// <summary>
/// Service for broadcasting activity events to connected admin clients
/// </summary>
public interface IActivityBroadcaster
{
    Task BroadcastPostCreated(string userId, string username, string postId, string discussionId, string discussionTitle, string? communityName, string? hubName, string? spaceName);
    Task BroadcastDiscussionCreated(string userId, string username, string discussionId, string discussionTitle, string? communityName, string? hubName, string? spaceName);
    Task BroadcastReactionAdded(string userId, string username, string reactionType, string targetType, string targetId, string? targetTitle);
    Task BroadcastFollowAdded(string userId, string username, string targetType, string targetId, string? targetName);
    Task BroadcastUserRegistered(string userId, string username, string email);
    Task BroadcastModerationAction(string moderatorId, string moderatorName, string action, string targetType, string? targetId, string? targetName, string? reason);
    Task BroadcastUserBanned(string moderatorId, string moderatorName, string userId, string username, string reason);
    Task BroadcastUserUnbanned(string moderatorId, string moderatorName, string userId, string username);
    Task BroadcastContentDeleted(string moderatorId, string moderatorName, string contentType, string contentId, string? reason);
}
