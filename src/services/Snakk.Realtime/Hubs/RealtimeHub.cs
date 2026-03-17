using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;

namespace Snakk.Realtime.Hubs;

/// <summary>
/// SignalR hub for real-time updates to browser clients
/// Browsers connect here via WebSocket
/// API posts events via HTTP to broadcast to connected clients
/// </summary>
public class RealtimeHub(ILogger<RealtimeHub> logger) : Hub
{
    // Viewer count tracking: discussionId → count
    private static readonly ConcurrentDictionary<string, int> ViewerCounts = new();

    // Connection → discussion mapping for cleanup on disconnect
    private static readonly ConcurrentDictionary<string, string> ConnectionDiscussions = new();

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
        // Track previous discussion for this connection (unsubscribe if switching)
        if (ConnectionDiscussions.TryGetValue(Context.ConnectionId, out var previousId) && previousId != discussionId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"discussion:{previousId}");
            DecrementViewerCount(previousId);
            await BroadcastViewerCount(previousId);
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"discussion:{discussionId}");
        ConnectionDiscussions[Context.ConnectionId] = discussionId;

        IncrementViewerCount(discussionId);
        await BroadcastViewerCount(discussionId);

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
        ConnectionDiscussions.TryRemove(Context.ConnectionId, out _);

        DecrementViewerCount(discussionId);
        await BroadcastViewerCount(discussionId);

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

    // ==================== Typing Indicators ====================

    /// <summary>
    /// Notify others that this user is typing in a discussion
    /// </summary>
    public async Task StartTyping(string discussionId, string displayName)
    {
        await Clients.OthersInGroup($"discussion:{discussionId}")
            .SendAsync("ReceiveTyping", new { displayName, isTyping = true });
    }

    /// <summary>
    /// Notify others that this user stopped typing
    /// </summary>
    public async Task StopTyping(string discussionId, string displayName)
    {
        await Clients.OthersInGroup($"discussion:{discussionId}")
            .SendAsync("ReceiveTyping", new { displayName, isTyping = false });
    }

    // ==================== Lifecycle ====================

    public override async Task OnConnectedAsync()
    {
        logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // Clean up viewer count for this connection's discussion
        if (ConnectionDiscussions.TryRemove(Context.ConnectionId, out var discussionId))
        {
            DecrementViewerCount(discussionId);
            await BroadcastViewerCount(discussionId);
        }

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

    // ==================== Viewer Count Helpers ====================

    private static void IncrementViewerCount(string discussionId) =>
        ViewerCounts.AddOrUpdate(discussionId, 1, (_, count) => count + 1);

    private static void DecrementViewerCount(string discussionId) =>
        ViewerCounts.AddOrUpdate(discussionId, 0, (_, count) => Math.Max(0, count - 1));

    private async Task BroadcastViewerCount(string discussionId)
    {
        var count = ViewerCounts.GetValueOrDefault(discussionId, 0);
        await Clients.Group($"discussion:{discussionId}")
            .SendAsync("ReceiveViewerCount", new { count });
    }
}
