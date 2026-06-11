namespace Snakk.Infrastructure.Services;

using Microsoft.AspNetCore.SignalR;
using Snakk.Application.Services;
using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Infrastructure.Hubs;

/// <summary>
/// SignalR implementation of IRealtimeNotifier.
/// Fetches routing context and renders HTML for realtime updates.
/// </summary>
public class SignalRRealtimeNotifier(
    IHubContext<SnakkHub> hubContext,
    IPostHtmlRenderer htmlRenderer) : IRealtimeNotifier
{

    public async Task NotifyPostCreatedAsync(Post post, User author, Discussion discussion) =>
        await hubContext.Clients
            .Group($"discussion:{discussion.PublicId.Value}")
            .SendAsync("ReceiveUpdate", new
            {
                group = $"discussion:{discussion.PublicId.Value}",
                eventType = "post-created",
                postId = post.PublicId.Value,
                discussionId = discussion.PublicId.Value
            });

    public async Task NotifyPostEditedAsync(Post post, User author, Discussion discussion) =>
        await hubContext.Clients
            .Group($"discussion:{discussion.PublicId.Value}")
            .SendAsync("ReceiveUpdate", new
            {
                group = $"discussion:{discussion.PublicId.Value}",
                eventType = "post-edited",
                postId = post.PublicId.Value,
                discussionId = discussion.PublicId.Value
            });

    public async Task NotifyPostDeletedAsync(
        PostId postId,
        DiscussionId discussionId,
        bool isHardDelete)
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
            html = htmlRenderer.RenderTombstone();
            swapStrategy = "outerHTML";
        }

        // Send to discussion subscribers
        await hubContext.Clients
            .Group($"discussion:{discussionId.Value}")
            .SendAsync("ReceiveUpdate", new
            {
                group = $"discussion:{discussionId.Value}",
                eventType = "post-deleted",
                htmlContent = html,
                targetId = $"post-{postId.Value}",
                swapStrategy
            });
    }

    public async Task NotifyUserAsync(UserId userId, object notification) =>
        await hubContext.Clients
            .Group($"user:{userId.Value}")
            .SendAsync("ReceiveNotification", notification);

    public async Task NotifyReactionUpdatedAsync(
        PostId postId,
        DiscussionId discussionId,
        Dictionary<ReactionType, int> counts)
    {
        // Convert enum keys to strings for JSON serialization
        var countDict = counts.ToDictionary(
            kvp => kvp.Key.ToString(),
            kvp => kvp.Value);

        await hubContext.Clients
            .Group($"discussion:{discussionId.Value}")
            .SendAsync("ReceiveUpdate", new
            {
                group = $"discussion:{discussionId.Value}",
                eventType = "reaction-updated",
                postId = postId.Value,
                counts = countDict
            });
    }

    public async Task NotifyUnreadCountUpdatedAsync(UserId userId, int count) =>
        await hubContext.Clients
            .Group($"user:{userId.Value}")
            .SendAsync("ReceiveNotificationCount", new { unreadCount = count });

    public async Task NotifyDiscussionLockedAsync(DiscussionId discussionId) =>
        await hubContext.Clients
            .Group($"discussion:{discussionId.Value}")
            .SendAsync("ReceiveUpdate", new
            {
                group = $"discussion:{discussionId.Value}",
                eventType = "discussion-locked",
                targetId = "discussion-lock-banner",
                htmlContent = "",
                swapStrategy = ""
            });

    public async Task NotifyDiscussionUnlockedAsync(DiscussionId discussionId) =>
        await hubContext.Clients
            .Group($"discussion:{discussionId.Value}")
            .SendAsync("ReceiveUpdate", new
            {
                group = $"discussion:{discussionId.Value}",
                eventType = "discussion-unlocked",
                targetId = "discussion-lock-banner",
                htmlContent = "",
                swapStrategy = ""
            });

    public async Task NotifyDiscussionCreatedAsync(DiscussionId discussionId, SpaceId spaceId, HubId hubId)
    {
        var payload = new { eventType = "discussion-created", discussionId = discussionId.Value, spaceId = spaceId.Value, hubId = hubId.Value };
        await Task.WhenAll(
            hubContext.Clients.Group($"space:{spaceId.Value}").SendAsync("ReceiveUpdate", payload),
            hubContext.Clients.Group($"hub:{hubId.Value}").SendAsync("ReceiveUpdate", payload)
        );
    }

    public async Task NotifyGlobalAsync(string eventType, string message) =>
        await hubContext.Clients
            .Group("global")
            .SendAsync("ReceiveUpdate", new
            {
                group = "global",
                eventType,
                targetId = "",
                htmlContent = message,
                swapStrategy = ""
            });

    public async Task NotifyDiscussionPinnedAsync(DiscussionId discussionId, SpaceId spaceId, bool isPinned) =>
        await hubContext.Clients
            .Group($"space:{spaceId.Value}")
            .SendAsync("ReceiveUpdate", new
            {
                group = $"space:{spaceId.Value}",
                eventType = isPinned ? "discussion-pinned" : "discussion-unpinned",
                discussionId = discussionId.Value,
                htmlContent = "",
                swapStrategy = ""
            });

    public async Task NotifyDiscussionDeletedAsync(DiscussionId discussionId, SpaceId spaceId) =>
        await hubContext.Clients
            .Group($"space:{spaceId.Value}")
            .SendAsync("ReceiveUpdate", new
            {
                group = $"space:{spaceId.Value}",
                eventType = "discussion-deleted",
                discussionId = discussionId.Value,
                htmlContent = "",
                swapStrategy = ""
            });

    public async Task NotifyDiscussionTitleUpdatedAsync(DiscussionId discussionId, string newTitle) =>
        await hubContext.Clients
            .Group($"discussion:{discussionId.Value}")
            .SendAsync("ReceiveUpdate", new
            {
                group = $"discussion:{discussionId.Value}",
                eventType = "discussion-title-updated",
                title = newTitle,
                htmlContent = "",
                swapStrategy = ""
            });

    public async Task NotifyBannerUpdatedAsync(string scopeType, string scopePublicId)
    {
        var group = scopeType switch
        {
            "Community" => $"community:{scopePublicId}",
            "Hub"       => $"hub:{scopePublicId}",
            "Space"     => $"space:{scopePublicId}",
            _           => $"space:{scopePublicId}"
        };

        await hubContext.Clients
            .Group(group)
            .SendAsync("ReceiveUpdate", new
            {
                group,
                eventType = "banner-updated",
                htmlContent = "",
                swapStrategy = ""
            });
    }

    public async Task NotifyReadStateUpdatedAsync(UserId userId, string discussionId, string postId) =>
        await hubContext.Clients
            .Group($"user:{userId.Value}")
            .SendAsync("ReceiveUpdate", new
            {
                group = $"user:{userId.Value}",
                eventType = "read-state-updated",
                discussionId,
                postId,
                htmlContent = "",
                swapStrategy = ""
            });

    public async Task NotifyPostCountUpdatedAsync(
        DiscussionId discussionId, SpaceId spaceId, HubId? hubId, int delta,
        string? lastPostExcerpt = null, string? lastReplierId = null, string? lastReplierName = null,
        string? lastReplierAvatarUrl = null, long? lastActivityAtUnix = null)
    {
        var payload = new { eventType = "post-count-updated", discussionId = discussionId.Value, delta,
            lastPostExcerpt, lastReplierId, lastReplierName, lastReplierAvatarUrl, lastActivityAtUnix,
            htmlContent = "", swapStrategy = "" };
        var broadcasts = new List<Task>
        {
            hubContext.Clients.Group($"space:{spaceId.Value}").SendAsync("ReceiveUpdate", payload)
        };
        if (hubId is not null)
            broadcasts.Add(hubContext.Clients.Group($"hub:{hubId.Value}").SendAsync("ReceiveUpdate", payload));
        await Task.WhenAll(broadcasts);
    }

    public async Task NotifyDiscussionReactionCountAsync(DiscussionId discussionId, SpaceId spaceId, int delta) =>
        await hubContext.Clients
            .Group($"space:{spaceId.Value}")
            .SendAsync("ReceiveUpdate", new
            {
                group = $"space:{spaceId.Value}",
                eventType = "discussion-reaction-count-updated",
                discussionId = discussionId.Value,
                delta,
                htmlContent = "",
                swapStrategy = ""
            });

    public async Task NotifyDebateUpdatedAsync(DiscussionId discussionId, IReadOnlyList<DebatePositionUpdate> positions) =>
        await hubContext.Clients
            .Group($"discussion:{discussionId.Value}")
            .SendAsync("ReceiveUpdate", new
            {
                group = $"discussion:{discussionId.Value}",
                eventType = "debate-updated",
                discussionId = discussionId.Value,
                debatePositions = positions.Select(p => new { index = p.Index, label = p.Label, postCount = p.PostCount, pct = p.Pct }),
                htmlContent = "",
                swapStrategy = ""
            });

    public async Task NotifyPollUpdatedAsync(DiscussionId discussionId, IReadOnlyList<PollOptionUpdate> options, int totalVotes) =>
        await hubContext.Clients
            .Group($"discussion:{discussionId.Value}")
            .SendAsync("ReceiveUpdate", new
            {
                group = $"discussion:{discussionId.Value}",
                eventType = "poll-updated",
                discussionId = discussionId.Value,
                pollOptions = options.Select(o => new { text = o.Text, voteCount = o.VoteCount, pct = o.Pct }),
                totalVotes,
                htmlContent = "",
                swapStrategy = ""
            });

    public async Task NotifyDirectMessageCountUpdatedAsync(string userPublicId, int userIntId, int unreadCount) =>
        await hubContext.Clients
            .Group($"user:{userPublicId}")
            .SendAsync("ReceiveDirectMessageCount", new { unreadCount });

    public async Task NotifyDmMessagesDeletedAsync(string recipientPublicId, int recipientIntId, string conversationId, IReadOnlyList<string> messageIds) =>
        await hubContext.Clients
            .Group($"user:{recipientPublicId}")
            .SendAsync("ReceiveDmMessagesDeleted", new { conversationId, messageIds });
}
