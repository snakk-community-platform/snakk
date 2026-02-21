namespace Snakk.Api.Hubs;

using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;
using Snakk.Application.Services;

/// <summary>
/// SignalR hub for broadcasting real-time platform activity to admin clients
/// </summary>
public class ActivityHub : Hub
{
    private static readonly string AdminGroup = "Admins";

    public override async Task OnConnectedAsync()
    {
        // Only allow admin users to connect
        if (Context.User?.IsInRole("Admin") == true)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, AdminGroup);
        }
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.User?.IsInRole("Admin") == true)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, AdminGroup);
        }
        await base.OnDisconnectedAsync(exception);
    }
}

public class ActivityBroadcaster : IActivityBroadcaster
{
    private readonly IHubContext<ActivityHub> _hubContext;

    public ActivityBroadcaster(IHubContext<ActivityHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task BroadcastPostCreated(string userId, string username, string postId, string discussionId, string discussionTitle, string? communityName, string? hubName, string? spaceName)
    {
        await _hubContext.Clients.Group("Admins").SendAsync("ActivityEvent", new
        {
            id = Guid.NewGuid().ToString("N"),
            type = "post",
            category = "content",
            userId,
            username,
            targetId = postId,
            targetType = "discussion",
            targetName = discussionTitle,
            communityName,
            hubName,
            spaceName,
            action = $"Posted in {discussionTitle}",
            timestamp = DateTime.UtcNow
        });
    }

    public async Task BroadcastDiscussionCreated(string userId, string username, string discussionId, string discussionTitle, string? communityName, string? hubName, string? spaceName)
    {
        await _hubContext.Clients.Group("Admins").SendAsync("ActivityEvent", new
        {
            id = Guid.NewGuid().ToString("N"),
            type = "discussion",
            category = "content",
            userId,
            username,
            targetId = discussionId,
            targetType = "discussion",
            targetName = discussionTitle,
            communityName,
            hubName,
            spaceName,
            action = $"Created discussion: {discussionTitle}",
            timestamp = DateTime.UtcNow
        });
    }

    public async Task BroadcastReactionAdded(string userId, string username, string reactionType, string targetType, string targetId, string? targetTitle)
    {
        await _hubContext.Clients.Group("Admins").SendAsync("ActivityEvent", new
        {
            id = Guid.NewGuid().ToString("N"),
            type = "reaction",
            category = "social",
            userId,
            username,
            targetId,
            targetType,
            targetName = targetTitle,
            action = $"Reacted with {reactionType} to {targetType}",
            details = reactionType,
            timestamp = DateTime.UtcNow
        });
    }

    public async Task BroadcastFollowAdded(string userId, string username, string targetType, string targetId, string? targetName)
    {
        await _hubContext.Clients.Group("Admins").SendAsync("ActivityEvent", new
        {
            id = Guid.NewGuid().ToString("N"),
            type = "follow",
            category = "social",
            userId,
            username,
            targetId,
            targetType,
            targetName,
            action = $"Followed {targetType}: {targetName ?? targetId}",
            timestamp = DateTime.UtcNow
        });
    }

    public async Task BroadcastUserRegistered(string userId, string username, string email)
    {
        await _hubContext.Clients.Group("Admins").SendAsync("ActivityEvent", new
        {
            id = Guid.NewGuid().ToString("N"),
            type = "user",
            category = "user",
            userId,
            username,
            action = $"New user registered: {username}",
            details = email,
            timestamp = DateTime.UtcNow
        });
    }

    public async Task BroadcastModerationAction(string moderatorId, string moderatorName, string action, string targetType, string? targetId, string? targetName, string? reason)
    {
        await _hubContext.Clients.Group("Admins").SendAsync("ActivityEvent", new
        {
            id = Guid.NewGuid().ToString("N"),
            type = "moderation",
            category = "moderation",
            userId = moderatorId,
            username = moderatorName,
            targetId,
            targetType,
            targetName,
            action = $"{action} {targetType}",
            details = reason,
            timestamp = DateTime.UtcNow
        });
    }

    public async Task BroadcastUserBanned(string moderatorId, string moderatorName, string userId, string username, string reason)
    {
        await _hubContext.Clients.Group("Admins").SendAsync("ActivityEvent", new
        {
            id = Guid.NewGuid().ToString("N"),
            type = "moderation",
            category = "moderation",
            userId = moderatorId,
            username = moderatorName,
            targetId = userId,
            targetType = "user",
            targetName = username,
            action = $"Banned user: {username}",
            details = reason,
            timestamp = DateTime.UtcNow
        });
    }

    public async Task BroadcastUserUnbanned(string moderatorId, string moderatorName, string userId, string username)
    {
        await _hubContext.Clients.Group("Admins").SendAsync("ActivityEvent", new
        {
            id = Guid.NewGuid().ToString("N"),
            type = "moderation",
            category = "moderation",
            userId = moderatorId,
            username = moderatorName,
            targetId = userId,
            targetType = "user",
            targetName = username,
            action = $"Unbanned user: {username}",
            timestamp = DateTime.UtcNow
        });
    }

    public async Task BroadcastContentDeleted(string moderatorId, string moderatorName, string contentType, string contentId, string? reason)
    {
        await _hubContext.Clients.Group("Admins").SendAsync("ActivityEvent", new
        {
            id = Guid.NewGuid().ToString("N"),
            type = "moderation",
            category = "moderation",
            userId = moderatorId,
            username = moderatorName,
            targetId = contentId,
            targetType = contentType,
            action = $"Deleted {contentType}",
            details = reason,
            timestamp = DateTime.UtcNow
        });
    }
}
