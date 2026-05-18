using System.Collections.Concurrent;
using System.Security.Claims;
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
    // Connection → (userId, displayName, isAnon, avatarUrl) for viewer presence
    private static readonly ConcurrentDictionary<string, (string UserId, string DisplayName, bool IsAnon, string AvatarUrl)> ConnectionViewers = new();

    // Connection → discussion mapping for viewer cleanup on disconnect
    private static readonly ConcurrentDictionary<string, string> ConnectionDiscussions = new();

    // Connection → discussionId for typing cleanup on disconnect
    private static readonly ConcurrentDictionary<string, string> TypingConnections = new();

    /// <summary>Current number of tracked connections (for health checks)</summary>
    public static int ActiveConnectionCount => ConnectionDiscussions.Count;

    /// <summary>
    /// Periodic cleanup: removes ConnectionViewers entries whose connection is no longer
    /// in any discussion. Called by ViewerCountCleanupService to prevent unbounded growth
    /// from connections that disconnected without triggering OnDisconnectedAsync.
    /// </summary>
    public static int CleanupStaleEntries()
    {
        var removed = 0;
        foreach (var connId in ConnectionViewers.Keys)
        {
            if (!ConnectionDiscussions.ContainsKey(connId) && ConnectionViewers.TryRemove(connId, out _))
                removed++;
        }
        return removed;
    }

    // ==================== Lifecycle ====================

    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;
        if (userId is not null)
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");

        logger.LogInformation("Client connected: {ConnectionId} userId={UserId}", Context.ConnectionId, userId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (ConnectionDiscussions.TryRemove(Context.ConnectionId, out var discussionId))
        {
            ConnectionViewers.TryRemove(Context.ConnectionId, out _);
            await BroadcastViewers(discussionId);
        }

        if (TypingConnections.TryRemove(Context.ConnectionId, out var typingDiscussionId))
        {
            var userId = Context.UserIdentifier ?? "";
            var displayName = Context.User?.FindFirst(ClaimTypes.Name)?.Value ?? "";
            var isAnon = Context.User?.FindFirst("presence_hidden")?.Value == "true";
            await Clients.OthersInGroup($"discussion:{typingDiscussionId}")
                .SendAsync("ReceiveTyping", new { userId, displayName, isTyping = false, isAnon, group = $"discussion:{typingDiscussionId}" });
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
        var displayName = Context.User?.FindFirst(ClaimTypes.Name)?.Value ?? "";
        var isAnon = Context.User?.FindFirst("presence_hidden")?.Value == "true";
        var avatarUrl = Context.User?.FindFirst("avatar_url")?.Value ?? "";

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
            ConnectionViewers.TryRemove(Context.ConnectionId, out _);
            await BroadcastViewers(previousId);
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"discussion:{discussionId}");
        ConnectionDiscussions[Context.ConnectionId] = discussionId;
        ConnectionViewers[Context.ConnectionId] = (userId, displayName, isAnon, avatarUrl);

        await BroadcastViewers(discussionId);

        logger.LogInformation(
            "Client {ConnectionId} subscribed to discussion {DiscussionId}",
            Context.ConnectionId, discussionId);
    }

    /// <summary>Unsubscribe from discussion</summary>
    public async Task UnsubscribeFromDiscussion(string discussionId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"discussion:{discussionId}");
        ConnectionDiscussions.TryRemove(Context.ConnectionId, out _);
        ConnectionViewers.TryRemove(Context.ConnectionId, out _);

        await BroadcastViewers(discussionId);

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

    // ==================== Community ====================

    /// <summary>Subscribe to community updates (banner changes)</summary>
    public async Task SubscribeToCommunity(string communityPublicId)
    {
        var userId = Context.UserIdentifier!;

        if (!await accessVerifier.VerifyCommunityAccessAsync(userId, communityPublicId))
        {
            logger.LogWarning("Access denied: {UserId} → community:{CommunityPublicId}", userId, communityPublicId);
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"community:{communityPublicId}");
        logger.LogInformation(
            "Client {ConnectionId} subscribed to community {CommunityPublicId}",
            Context.ConnectionId, communityPublicId);
    }

    /// <summary>Unsubscribe from community</summary>
    public async Task UnsubscribeFromCommunity(string communityPublicId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"community:{communityPublicId}");
        logger.LogInformation(
            "Client {ConnectionId} unsubscribed from community {CommunityPublicId}",
            Context.ConnectionId, communityPublicId);
    }

    // ==================== Typing Indicators ====================

    /// <summary>Notify others that this user is composing a reply in a discussion</summary>
    public async Task StartTyping(string discussionId)
    {
        var userId = Context.UserIdentifier!;
        var displayName = Context.User?.FindFirst(ClaimTypes.Name)?.Value ?? "";
        var isAnon = Context.User?.FindFirst("presence_hidden")?.Value == "true";
        TypingConnections[Context.ConnectionId] = discussionId;
        await Clients.OthersInGroup($"discussion:{discussionId}")
            .SendAsync("ReceiveTyping", new { userId, displayName, isTyping = true, isAnon, group = $"discussion:{discussionId}" });
    }

    /// <summary>Notify others that this user stopped composing a reply</summary>
    public async Task StopTyping(string discussionId)
    {
        var userId = Context.UserIdentifier!;
        var displayName = Context.User?.FindFirst(ClaimTypes.Name)?.Value ?? "";
        var isAnon = Context.User?.FindFirst("presence_hidden")?.Value == "true";
        TypingConnections.TryRemove(Context.ConnectionId, out _);
        await Clients.OthersInGroup($"discussion:{discussionId}")
            .SendAsync("ReceiveTyping", new { userId, displayName, isTyping = false, isAnon, group = $"discussion:{discussionId}" });
    }

    // ==================== Viewer Presence ====================

    private async Task BroadcastViewers(string discussionId)
    {
        // Collect distinct users (deduplicate by userId — multiple tabs = one entry)
        var viewers = ConnectionDiscussions
            .Where(kvp => kvp.Value == discussionId)
            .Select(kvp => ConnectionViewers.TryGetValue(kvp.Key, out var v) ? ((string UserId, string DisplayName, bool IsAnon, string AvatarUrl)?)v : null)
            .Where(v => v.HasValue)
            .GroupBy(v => v!.Value.UserId)
            .Select(g => new {
                userId = g.Key,
                displayName = g.First()!.Value.DisplayName,
                isAnon = g.First()!.Value.IsAnon,
                avatarUrl = g.First()!.Value.AvatarUrl })
            .ToList();

        await Clients.Group($"discussion:{discussionId}")
            .SendAsync("ReceiveViewerCount", new { count = viewers.Count, viewers, group = $"discussion:{discussionId}" });
    }
}
