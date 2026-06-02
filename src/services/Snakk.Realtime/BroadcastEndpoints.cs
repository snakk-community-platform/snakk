using System.Text.RegularExpressions;
using Microsoft.AspNetCore.SignalR;
using Snakk.Realtime.Hubs;
using Snakk.Realtime.Models;

namespace Snakk.Realtime;

public static class BroadcastEndpoints
{
    private static readonly Regex ValidGroup = new(
        @"^(global|admin-activity|(discussion|space|hub|user|community):[a-zA-Z0-9_-]{1,64})$",
        RegexOptions.Compiled);

    /// <summary>
    /// Broadcast a realtime event to connected browser clients
    /// Called by Snakk.Api when posts/reactions/etc are created
    /// </summary>
    public static async Task<IResult> BroadcastEvent(
        BroadcastRequest request,
        IHubContext<RealtimeHub> hubContext)
    {
        if (!ValidGroup.IsMatch(request.TargetGroup))
            return Results.BadRequest(new { error = "Invalid target group" });

        // Special event types that need their own SignalR event name
        if (request.EventType == "notification-count")
        {
            await hubContext.Clients.Group(request.TargetGroup)
                .SendAsync("ReceiveNotificationCount", new { unreadCount = request.UnreadCount ?? 0 });
            return Results.Ok(new { success = true, targetGroup = request.TargetGroup });
        }

        if (request.EventType == "dm-count")
        {
            await hubContext.Clients.Group(request.TargetGroup)
                .SendAsync("ReceiveDirectMessageCount", new { unreadCount = request.UnreadCount ?? 0 });
            return Results.Ok(new { success = true, targetGroup = request.TargetGroup });
        }

        if (request.EventType == "dm-messages-deleted")
        {
            await hubContext.Clients.Group(request.TargetGroup)
                .SendAsync("ReceiveDmMessagesDeleted", new
                {
                    conversationId = request.ConversationId ?? "",
                    messageIds = request.MessageIds ?? []
                });
            return Results.Ok(new { success = true, targetGroup = request.TargetGroup });
        }

        await hubContext.Clients.Group(request.TargetGroup)
            .SendAsync("ReceiveUpdate", new
            {
                group = request.TargetGroup,
                eventType = request.EventType,
                htmlContent = request.HtmlContent,
                targetId = request.TargetId,
                swapStrategy = request.SwapStrategy,
                postId = request.PostId,
                counts = request.Counts,
                discussionId = request.DiscussionId,
                spaceId = request.SpaceId,
                hubId = request.HubId,
                title = request.Title,
                delta = request.Delta,
                authorId = request.AuthorId,
                authorName = request.AuthorName,
                debatePositions = request.DebatePositions?.Select(p => new { index = p.Index, label = p.Label, postCount = p.PostCount, pct = p.Pct }),
                pollOptions = request.PollOptions?.Select(p => new { text = p.Text, voteCount = p.VoteCount, pct = p.Pct }),
                totalVotes = request.TotalVotes,
                lastPostExcerpt = request.LastPostExcerpt,
                lastReplierId = request.LastReplierId,
                lastReplierName = request.LastReplierName,
                lastReplierAvatarUrl = request.LastReplierAvatarUrl,
                lastActivityAtUnix = request.LastActivityAtUnix
            });

        return Results.Ok(new { success = true, targetGroup = request.TargetGroup });
    }

    /// <summary>
    /// Broadcast an activity event to admin panel
    /// Called by Snakk.Api for admin activity feed
    /// </summary>
    public static async Task<IResult> BroadcastActivity(
        ActivityBroadcastRequest request,
        IHubContext<RealtimeHub> hubContext)
    {
        if (!ValidGroup.IsMatch(request.TargetGroup))
            return Results.BadRequest(new { error = "Invalid target group" });

        await hubContext.Clients.Group(request.TargetGroup)
            .SendAsync("ReceiveActivity", new
            {
                activityType = request.ActivityType,
                data = request.Data
            });

        return Results.Ok(new { success = true, targetGroup = request.TargetGroup });
    }
}
