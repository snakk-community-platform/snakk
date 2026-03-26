using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Snakk.Application.Services;
using Snakk.Domain.Entities;
using Snakk.Domain.ValueObjects;

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
            await _httpClient.PostAsJsonAsync("/api/broadcast", new
            {
                EventType = "post-created",
                TargetGroup = $"discussion:{discussion.PublicId}",
                PostId = post.PublicId.Value,
                DiscussionId = discussion.PublicId.Value
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
            await _httpClient.PostAsJsonAsync("/api/broadcast", new
            {
                EventType = "post-edited",
                TargetGroup = $"discussion:{discussion.PublicId}",
                PostId = post.PublicId.Value,
                DiscussionId = discussion.PublicId.Value
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

    public async Task NotifyDiscussionLockedAsync(DiscussionId discussionId)
    {
        try
        {
            await _httpClient.PostAsJsonAsync("/api/broadcast", new
            {
                EventType = "discussion-locked",
                TargetGroup = $"discussion:{discussionId.Value}",
                TargetId = "discussion-lock-banner",
                HtmlContent = "",
                SwapStrategy = ""
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to broadcast discussion locked: {DiscussionId}", discussionId.Value);
        }
    }

    public async Task NotifyDiscussionUnlockedAsync(DiscussionId discussionId)
    {
        try
        {
            await _httpClient.PostAsJsonAsync("/api/broadcast", new
            {
                EventType = "discussion-unlocked",
                TargetGroup = $"discussion:{discussionId.Value}",
                TargetId = "discussion-lock-banner",
                HtmlContent = "",
                SwapStrategy = ""
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to broadcast discussion unlocked: {DiscussionId}", discussionId.Value);
        }
    }

    public async Task NotifyDiscussionCreatedAsync(DiscussionId discussionId, SpaceId spaceId, User author)
    {
        try
        {
            await _httpClient.PostAsJsonAsync("/api/broadcast", new
            {
                EventType = "discussion-created",
                TargetGroup = $"space:{spaceId.Value}",
                DiscussionId = discussionId.Value,
                AuthorId = author.PublicId.Value,
                AuthorName = author.DisplayName
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to broadcast discussion created: {DiscussionId}", discussionId.Value);
        }
    }

    public async Task NotifyGlobalAsync(string eventType, string message)
    {
        try
        {
            await _httpClient.PostAsJsonAsync("/api/broadcast", new
            {
                EventType = eventType,
                TargetGroup = "global",
                TargetId = "",
                HtmlContent = message,
                SwapStrategy = ""
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to broadcast global: {EventType}", eventType);
        }
    }

    public async Task NotifyDiscussionPinnedAsync(DiscussionId discussionId, SpaceId spaceId, bool isPinned)
    {
        try
        {
            await _httpClient.PostAsJsonAsync("/api/broadcast", new
            {
                EventType = isPinned ? "discussion-pinned" : "discussion-unpinned",
                TargetGroup = $"space:{spaceId.Value}",
                DiscussionId = discussionId.Value,
                HtmlContent = "",
                SwapStrategy = ""
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to broadcast discussion pinned: {DiscussionId}", discussionId.Value);
        }
    }

    public async Task NotifyDiscussionDeletedAsync(DiscussionId discussionId, SpaceId spaceId)
    {
        try
        {
            await _httpClient.PostAsJsonAsync("/api/broadcast", new
            {
                EventType = "discussion-deleted",
                TargetGroup = $"space:{spaceId.Value}",
                DiscussionId = discussionId.Value,
                HtmlContent = "",
                SwapStrategy = ""
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to broadcast discussion deleted: {DiscussionId}", discussionId.Value);
        }
    }

    public async Task NotifyDiscussionTitleUpdatedAsync(DiscussionId discussionId, string newTitle)
    {
        try
        {
            await _httpClient.PostAsJsonAsync("/api/broadcast", new
            {
                EventType = "discussion-title-updated",
                TargetGroup = $"discussion:{discussionId.Value}",
                DiscussionId = discussionId.Value,
                Title = newTitle,
                HtmlContent = "",
                SwapStrategy = ""
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to broadcast discussion title updated: {DiscussionId}", discussionId.Value);
        }
    }

    public async Task NotifyBannerUpdatedAsync(string scopeType, string scopePublicId)
    {
        var group = scopeType switch
        {
            "Community" => $"community:{scopePublicId}",
            "Hub"       => $"hub:{scopePublicId}",
            "Space"     => $"space:{scopePublicId}",
            _           => $"space:{scopePublicId}"
        };

        try
        {
            await _httpClient.PostAsJsonAsync("/api/broadcast", new
            {
                EventType = "banner-updated",
                TargetGroup = group,
                HtmlContent = "",
                SwapStrategy = ""
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to broadcast banner updated: {ScopeType}/{ScopeId}", scopeType, scopePublicId);
        }
    }

    public async Task NotifyReadStateUpdatedAsync(UserId userId, string discussionId, string postId)
    {
        try
        {
            await _httpClient.PostAsJsonAsync("/api/broadcast", new
            {
                EventType = "read-state-updated",
                TargetGroup = $"user:{userId.Value}",
                DiscussionId = discussionId,
                PostId = postId,
                HtmlContent = "",
                SwapStrategy = ""
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to broadcast read state updated: {UserId}", userId.Value);
        }
    }

    public async Task NotifyPostCountUpdatedAsync(DiscussionId discussionId, SpaceId spaceId, int delta)
    {
        try
        {
            await _httpClient.PostAsJsonAsync("/api/broadcast", new
            {
                EventType = "post-count-updated",
                TargetGroup = $"space:{spaceId.Value}",
                DiscussionId = discussionId.Value,
                Delta = delta,
                HtmlContent = "",
                SwapStrategy = ""
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to broadcast post count updated: {DiscussionId}", discussionId.Value);
        }
    }
}
