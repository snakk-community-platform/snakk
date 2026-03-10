using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Snakk.Application.Services;
using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;
using Snakk.Shared.Helpers;

namespace Snakk.Infrastructure.Realtime;

/// <summary>
/// HTTP-based implementation of IRealtimeNotifier that posts events to a SignalR microservice
/// </summary>
public class HttpRealtimeNotifier(
    IHttpClientFactory httpClientFactory,
    ILogger<HttpRealtimeNotifier> logger) : IRealtimeNotifier
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("RealtimeService");

    public async Task NotifyPostCreatedAsync(Post post, User author, Discussion discussion)
    {
        try
        {
            var htmlContent = RenderPostHtml(post, author);

            await _httpClient.PostAsJsonAsync("/api/broadcast", new
            {
                EventType = "post-created",
                TargetGroup = $"discussion:{discussion.PublicId}",
                TargetId = "posts-container",
                HtmlContent = htmlContent,
                SwapStrategy = "beforeend"
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to broadcast post created: {PostId}", post.PublicId);
        }
    }

    public async Task NotifyPostEditedAsync(Post post, User author, Discussion discussion)
    {
        try
        {
            var htmlContent = RenderPostHtml(post, author);

            await _httpClient.PostAsJsonAsync("/api/broadcast", new
            {
                EventType = "post-edited",
                TargetGroup = $"post:{post.PublicId}",
                TargetId = $"post-{post.PublicId}",
                HtmlContent = htmlContent,
                SwapStrategy = "outerHTML"
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to broadcast post edited: {PostId}", post.PublicId);
        }
    }

    public async Task NotifyPostDeletedAsync(PostId postId, DiscussionId discussionId, bool isHardDelete)
    {
        try
        {
            await _httpClient.PostAsJsonAsync("/api/broadcast", new
            {
                EventType = "post-deleted",
                TargetGroup = $"discussion:{discussionId.Value}",
                TargetId = $"post-{postId.Value}",
                HtmlContent = "",
                SwapStrategy = "outerHTML"
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to broadcast post deleted: {PostId}", postId.Value);
        }
    }

    public async Task NotifyReactionUpdatedAsync(
        PostId postId,
        DiscussionId discussionId,
        Dictionary<ReactionType, int> counts)
    {
        try
        {
            // Convert ReactionType enum to string keys for JavaScript
            var countsDict = counts.ToDictionary(
                kvp => kvp.Key.ToString(),
                kvp => kvp.Value);

            await _httpClient.PostAsJsonAsync("/api/broadcast", new
            {
                EventType = "reaction-updated",
                TargetGroup = $"post:{postId.Value}",
                TargetId = $"reactions-{postId.Value}",
                PostId = postId.Value,
                Counts = countsDict,
                HtmlContent = "",
                SwapStrategy = "innerHTML"
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to broadcast reaction updated: {PostId}", postId.Value);
        }
    }

    public async Task NotifyUnreadCountUpdatedAsync(UserId userId, int count)
    {
        try
        {
            await _httpClient.PostAsJsonAsync("/api/broadcast", new
            {
                EventType = "notification-count",
                TargetGroup = $"user:{userId.Value}",
                UnreadCount = count,
                HtmlContent = "",
                SwapStrategy = ""
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to broadcast notification count: {UserId}", userId.Value);
        }
    }

    public async Task NotifyUserAsync(UserId userId, object notification)
    {
        try
        {
            await _httpClient.PostAsJsonAsync("/api/broadcast", new
            {
                EventType = "notification",
                TargetGroup = $"user:{userId.Value}",
                Notification = notification,
                HtmlContent = "",
                SwapStrategy = ""
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to broadcast user notification: {UserId}", userId.Value);
        }
    }

    private string RenderPostHtml(Post post, User author)
    {
        var contentHtml = post.RenderedContent;

        // Generate post HTML (this matches your existing post card structure)
        return $@"
<div id=""post-{post.PublicId}"" class=""card bg-base-100 shadow-sm"">
    <div class=""card-body"">
        <div class=""flex items-start gap-3"">
            <div class=""avatar"">
                <div class=""w-10 h-10 rounded-full"">
                    <img src=""{AvatarHelper.GetAvatarUrl(author.PublicId, AvatarEntityType.User, 0)}"" alt=""{author.DisplayName}"" />
                </div>
            </div>
            <div class=""flex-1"">
                <div class=""flex items-center gap-2"">
                    <a href=""/users/{author.PublicId}"" class=""font-semibold hover:underline"">{author.DisplayName}</a>
                    <span class=""text-sm text-muted"">just now</span>
                </div>
                <div class=""prose prose-sm mt-2"">{contentHtml}</div>
                <div id=""reactions-{post.PublicId}"" class=""flex gap-2 mt-3"">
                    <button type=""button"" class=""reaction-pill add-reaction"" onclick=""toggleReactionPicker('{post.PublicId}')"" title=""Add reaction"">+</button>
                </div>
            </div>
        </div>
    </div>
</div>";
    }
}
