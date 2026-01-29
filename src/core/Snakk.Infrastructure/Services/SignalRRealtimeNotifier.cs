namespace Snakk.Infrastructure.Services;

using Microsoft.AspNetCore.SignalR;
using Snakk.Application.Services;
using Snakk.Domain.Entities;
using Snakk.Domain.Repositories;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Hubs;

/// <summary>
/// SignalR implementation of IRealtimeNotifier.
/// Fetches routing context and renders HTML for realtime updates.
/// </summary>
public class SignalRRealtimeNotifier : IRealtimeNotifier
{
    private readonly IHubContext<SnakkHub> _hubContext;
    private readonly IPostHtmlRenderer _htmlRenderer;
    private readonly ISpaceRepository _spaceRepository;
    private readonly IHubRepository _hubRepository;

    // Temporary user ID for rendering post cards
    private const string TempUserId = "01JJQP0000000000000000TEST";

    public SignalRRealtimeNotifier(
        IHubContext<SnakkHub> hubContext,
        IPostHtmlRenderer htmlRenderer,
        ISpaceRepository spaceRepository,
        IHubRepository hubRepository)
    {
        _hubContext = hubContext;
        _htmlRenderer = htmlRenderer;
        _spaceRepository = spaceRepository;
        _hubRepository = hubRepository;
    }

    public async Task NotifyPostCreatedAsync(Post post, User author, Discussion discussion)
    {
        // Fetch routing context
        var space = await _spaceRepository.GetByPublicIdAsync(discussion.SpaceId);
        if (space == null) return;

        var hub = await _hubRepository.GetByPublicIdAsync(space.HubId);
        if (hub == null) return;

        // Render HTML
        var html = _htmlRenderer.RenderPostCard(
            post,
            author,
            hub.Slug,
            space.Slug,
            discussion.Slug,
            TempUserId);

        // Send to discussion subscribers
        await _hubContext.Clients
            .Group($"discussion:{post.DiscussionId.Value}")
            .SendAsync("ReceiveUpdate", new
            {
                eventType = "post-created",
                htmlContent = html,
                targetId = "posts-container",
                swapStrategy = "beforeend"
            });
    }

    public async Task NotifyPostEditedAsync(Post post, User author, Discussion discussion)
    {
        // Fetch routing context
        var space = await _spaceRepository.GetByPublicIdAsync(discussion.SpaceId);
        if (space == null) return;

        var hub = await _hubRepository.GetByPublicIdAsync(space.HubId);
        if (hub == null) return;

        // Render entire post card with updated content
        var html = _htmlRenderer.RenderPostCard(
            post,
            author,
            hub.Slug,
            space.Slug,
            discussion.Slug,
            TempUserId);

        // Send to discussion subscribers
        await _hubContext.Clients
            .Group($"discussion:{post.DiscussionId.Value}")
            .SendAsync("ReceiveUpdate", new
            {
                eventType = "post-edited",
                htmlContent = html,
                targetId = $"post-{post.PublicId.Value}",
                swapStrategy = "outerHTML"
            });
    }

    public async Task NotifyPostDeletedAsync(PostId postId, DiscussionId discussionId, bool isHardDelete)
    {
        string html;
        string swapStrategy;

        if (isHardDelete)
        {
            // Hard delete - remove element completely
            html = "";
            swapStrategy = "outerHTML";
        }
        else
        {
            // Soft delete - replace with tombstone
            html = _htmlRenderer.RenderTombstone();
            swapStrategy = "outerHTML";
        }

        // Send to discussion subscribers
        await _hubContext.Clients
            .Group($"discussion:{discussionId.Value}")
            .SendAsync("ReceiveUpdate", new
            {
                eventType = "post-deleted",
                htmlContent = html,
                targetId = $"post-{postId.Value}",
                swapStrategy
            });
    }

    public async Task NotifyUserAsync(UserId userId, object notification)
    {
        await _hubContext.Clients
            .Group($"user:{userId.Value}")
            .SendAsync("ReceiveNotification", notification);
    }

    public async Task NotifyReactionUpdatedAsync(PostId postId, DiscussionId discussionId, Dictionary<ReactionType, int> counts)
    {
        // Convert enum keys to strings for JSON serialization
        var countDict = counts.ToDictionary(
            kvp => kvp.Key.ToString(),
            kvp => kvp.Value);

        await _hubContext.Clients
            .Group($"discussion:{discussionId.Value}")
            .SendAsync("ReceiveUpdate", new
            {
                eventType = "reaction-updated",
                postId = postId.Value,
                counts = countDict
            });
    }

    public async Task NotifyUnreadCountUpdatedAsync(UserId userId, int count)
    {
        await _hubContext.Clients
            .Group($"user:{userId.Value}")
            .SendAsync("ReceiveNotificationCount", new { unreadCount = count });
    }
}
