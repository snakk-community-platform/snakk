namespace Snakk.Infrastructure.Hubs;

using Microsoft.AspNetCore.SignalR;

/// <summary>
/// SignalR hub for realtime updates.
/// Clients can subscribe to groups at different levels: global, hub, space, discussion.
/// </summary>
public class SnakkHub : Hub
{
    /// <summary>
    /// Subscribe to global notifications (all activity across the platform)
    /// </summary>
    public async Task SubscribeToGlobal()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "global");
    }

    /// <summary>
    /// Subscribe to a specific hub's activity
    /// </summary>
    public async Task SubscribeToHub(string hubSlug)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"hub:{hubSlug}");
    }

    /// <summary>
    /// Subscribe to a specific space's activity
    /// </summary>
    public async Task SubscribeToSpace(string hubSlug, string spaceSlug)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"space:{hubSlug}:{spaceSlug}");
    }

    /// <summary>
    /// Subscribe to a specific discussion's activity
    /// </summary>
    public async Task SubscribeToDiscussion(string discussionId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"discussion:{discussionId}");
    }

    /// <summary>
    /// Unsubscribe from a discussion (for cleanup when leaving page)
    /// </summary>
    public async Task UnsubscribeFromDiscussion(string discussionId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"discussion:{discussionId}");
    }

    /// <summary>
    /// Unsubscribe from a space
    /// </summary>
    public async Task UnsubscribeFromSpace(string hubSlug, string spaceSlug)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"space:{hubSlug}:{spaceSlug}");
    }

    /// <summary>
    /// Unsubscribe from a hub
    /// </summary>
    public async Task UnsubscribeFromHub(string hubSlug)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"hub:{hubSlug}");
    }

    /// <summary>
    /// Subscribe to user-specific notifications
    /// </summary>
    public async Task SubscribeToUserNotifications(string userId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");
    }

    /// <summary>
    /// Unsubscribe from user-specific notifications
    /// </summary>
    public async Task UnsubscribeFromUserNotifications(string userId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user:{userId}");
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        // Groups are automatically cleaned up by SignalR on disconnect
        await base.OnDisconnectedAsync(exception);
    }
}
