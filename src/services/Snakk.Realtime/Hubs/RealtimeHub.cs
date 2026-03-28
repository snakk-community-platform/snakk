using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Snakk.Realtime.Services;

namespace Snakk.Realtime.Hubs;

/// <summary>
/// SignalR hub for real-time updates to browser clients.
/// Requires JWT authentication — anonymous connections are rejected.
/// User notification group is auto-subscribed server-side on connect.
/// </summary>
[Authorize]
public class RealtimeHub(IAccessVerifier accessVerifier, ILogger<RealtimeHub> logger) : Hub
{
    // Viewer count tracking: discussionId → count
    private static readonly ConcurrentDictionary<string, int> ViewerCounts = new();

    // Connection → discussion mapping for cleanup on disconnect
    private static readonly ConcurrentDictionary<string, string> ConnectionDiscussions = new();

    /// <summary>Current number of tracked connections (for health checks)</summary>
    public static int ActiveConnectionCount => ConnectionDiscussions.Count;

    /// <summary>
    /// Periodic cleanup: removes viewer count entries that have dropped to zero.
    /// Called by ViewerCountCleanupService to prevent unbounded dictionary growth
    /// from connections that disconnected without triggering OnDisconnectedAsync.
    /// </summary>
    public static int CleanupStaleEntries()
    {
        var removed = 0;
        foreach (var kvp in ViewerCounts)
        {
            if (kvp.Value <= 0 && ViewerCounts.TryRemove(kvp.Key, out _))
                removed++;
        }

        return removed;
    }

    // ==================== Lifecycle ====================

    public override async Task OnConnectedAsync()
    {
        // Auto-subscribe to personal notification group — server-controlled, can't be spoofed
        var userId = Context.UserIdentifier;
        if (userId is not null)
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");

        logger.LogInformation("Client connected: {ConnectionId} userId={UserId}", Context.ConnectionId, userId);
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
            logger.LogWarning(exception, "Client disconnected with error: {ConnectionId}", Context.ConnectionId);
        else
            logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);

        await base.OnDisconnectedAsync(exception);
    }

    // ==================== Global ====================

    /// <summary>Subscribe to global server announcements (maintenance, alerts)</summary>
    public async Task SubscribeToGlobal()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "global");
        logger.LogInformation("Client {ConnectionId} subscribed to global", Context.ConnectionId);
    }

    // ==================== Discussion ====================

    /// <summary>Subscribe to discussion updates (new posts, edits, reactions)</summary>
    public async Task SubscribeToDiscussion(string discussionId)
    {
        var userId = Context.UserIdentifier!;

        if (!await accessVerifier.VerifyDiscussionAccessAsync(userId, discussionId))
        {
            logger.LogWarning("Access denied: {UserId} → discussion:{DiscussionId}", userId, discussionId);
            return;
        }

        // Unsubscribe from previous discussion if switching
        if (ConnectionDiscussions.TryGetValue(Context.ConnectionId, out var previousId)
            && previousId != discussionId)
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
            Context.ConnectionId, discussionId);
    }

    /// <summary>Unsubscribe from discussion</summary>
    public async Task UnsubscribeFromDiscussion(string discussionId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"discussion:{discussionId}");
        ConnectionDiscussions.TryRemove(Context.ConnectionId, out _);

        DecrementViewerCount(discussionId);
        await BroadcastViewerCount(discussionId);

        logger.LogInformation(
            "Client {ConnectionId} unsubscribed from discussion {DiscussionId}",
            Context.ConnectionId, discussionId);
    }

    // ==================== Space ====================

    /// <summary>Subscribe to space updates (new discussions)</summary>
    public async Task SubscribeToSpace(string spacePublicId)
    {
        var userId = Context.UserIdentifier!;

        if (!await accessVerifier.VerifySpaceAccessAsync(userId, spacePublicId))
        {
            logger.LogWarning("Access denied: {UserId} → space:{SpacePublicId}", userId, spacePublicId);
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"space:{spacePublicId}");
        logger.LogInformation(
            "Client {ConnectionId} subscribed to space {SpacePublicId}",
            Context.ConnectionId, spacePublicId);
    }

    /// <summary>Unsubscribe from space</summary>
    public async Task UnsubscribeFromSpace(string spacePublicId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"space:{spacePublicId}");
        logger.LogInformation(
            "Client {ConnectionId} unsubscribed from space {SpacePublicId}",
            Context.ConnectionId, spacePublicId);
    }

    // ==================== Hub ====================

    /// <summary>Subscribe to hub updates</summary>
    public async Task SubscribeToHub(string hubPublicId)
    {
        var userId = Context.UserIdentifier!;

        if (!await accessVerifier.VerifyHubAccessAsync(userId, hubPublicId))
        {
            logger.LogWarning("Access denied: {UserId} → hub:{HubPublicId}", userId, hubPublicId);
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"hub:{hubPublicId}");
        logger.LogInformation(
            "Client {ConnectionId} subscribed to hub {HubPublicId}",
            Context.ConnectionId, hubPublicId);
    }

    /// <summary>Unsubscribe from hub</summary>
    public async Task UnsubscribeFromHub(string hubPublicId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"hub:{hubPublicId}");
        logger.LogInformation(
            "Client {ConnectionId} unsubscribed from hub {HubPublicId}",
            Context.ConnectionId, hubPublicId);
    }

    // ==================== Typing Indicators ====================

    /// <summary>Notify others that this user is typing in a discussion</summary>
    public async Task StartTyping(string discussionId, string displayName)
    {
        await Clients.OthersInGroup($"discussion:{discussionId}")
            .SendAsync("ReceiveTyping", new { displayName, isTyping = true, group = $"discussion:{discussionId}" });
    }

    /// <summary>Notify others that this user stopped typing</summary>
    public async Task StopTyping(string discussionId, string displayName)
    {
        await Clients.OthersInGroup($"discussion:{discussionId}")
            .SendAsync("ReceiveTyping", new { displayName, isTyping = false, group = $"discussion:{discussionId}" });
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
            .SendAsync("ReceiveViewerCount", new { count, group = $"discussion:{discussionId}" });
    }
}
