using Microsoft.AspNetCore.SignalR;

namespace Snakk.Realtime.Hubs;

/// <summary>
/// SignalR hub for real-time updates to browser clients
/// Browsers connect here via WebSocket
/// API posts events via HTTP to broadcast to connected clients
/// </summary>
public class RealtimeHub(ILogger<RealtimeHub> logger) : Hub
{
    /// <summary>
    /// Subscribe to global updates
    /// </summary>
    public async Task SubscribeToGlobal()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "global");
        logger.LogInformation("Client {ConnectionId} subscribed to global", Context.ConnectionId);
    }

    /// <summary>
    /// Subscribe to discussion updates
    /// </summary>
    public async Task SubscribeToDiscussion(string discussionId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"discussion:{discussionId}");
        logger.LogInformation(
            "Client {ConnectionId} subscribed to discussion {DiscussionId}",
            Context.ConnectionId,
            discussionId);
    }

    /// <summary>
    /// Unsubscribe from discussion
    /// </summary>
    public async Task UnsubscribeFromDiscussion(string discussionId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"discussion:{discussionId}");
        logger.LogInformation(
            "Client {ConnectionId} unsubscribed from discussion {DiscussionId}",
            Context.ConnectionId,
            discussionId);
    }

    /// <summary>
    /// Subscribe to space updates
    /// </summary>
    public async Task SubscribeToSpace(string hubSlug, string spaceSlug)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"space:{hubSlug}:{spaceSlug}");
        logger.LogInformation(
            "Client {ConnectionId} subscribed to space {HubSlug}/{SpaceSlug}",
            Context.ConnectionId,
            hubSlug,
            spaceSlug);
    }

    /// <summary>
    /// Unsubscribe from space
    /// </summary>
    public async Task UnsubscribeFromSpace(string hubSlug, string spaceSlug)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"space:{hubSlug}:{spaceSlug}");
        logger.LogInformation(
            "Client {ConnectionId} unsubscribed from space {HubSlug}/{SpaceSlug}",
            Context.ConnectionId,
            hubSlug,
            spaceSlug);
    }

    /// <summary>
    /// Subscribe to hub updates
    /// </summary>
    public async Task SubscribeToHub(string hubSlug)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"hub:{hubSlug}");
        logger.LogInformation(
            "Client {ConnectionId} subscribed to hub {HubSlug}",
            Context.ConnectionId,
            hubSlug);
    }

    /// <summary>
    /// Unsubscribe from hub
    /// </summary>
    public async Task UnsubscribeFromHub(string hubSlug)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"hub:{hubSlug}");
        logger.LogInformation(
            "Client {ConnectionId} unsubscribed from hub {HubSlug}",
            Context.ConnectionId,
            hubSlug);
    }

    /// <summary>
    /// Subscribe to user-specific notifications
    /// </summary>
    public async Task SubscribeToUserNotifications(string userId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");
        logger.LogInformation(
            "Client {ConnectionId} subscribed to user notifications for {UserId}",
            Context.ConnectionId,
            userId);
    }

    public override async Task OnConnectedAsync()
    {
        logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (exception is not null)
        {
            logger.LogWarning(exception, "Client disconnected with error: {ConnectionId}", Context.ConnectionId);
        }
        else
        {
            logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }
}
