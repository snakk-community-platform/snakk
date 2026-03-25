using Microsoft.AspNetCore.SignalR;
using Snakk.Realtime.Hubs;
using Snakk.Realtime.Models;

namespace Snakk.Realtime;

public static class BroadcastEndpoints
{
    /// <summary>
    /// Broadcast a realtime event to connected browser clients
    /// Called by Snakk.Api when posts/reactions/etc are created
    /// </summary>
    public static async Task<IResult> BroadcastEvent(
        BroadcastRequest request,
        IHubContext<RealtimeHub> hubContext)
    {
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
                title = request.Title,
                delta = request.Delta,
                authorId = request.AuthorId,
                authorName = request.AuthorName
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
        await hubContext.Clients.Group(request.TargetGroup)
            .SendAsync("ReceiveActivity", new
            {
                activityType = request.ActivityType,
                data = request.Data
            });

        return Results.Ok(new { success = true, targetGroup = request.TargetGroup });
    }
}
