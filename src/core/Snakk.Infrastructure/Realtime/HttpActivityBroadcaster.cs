using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Snakk.Application.Services;

namespace Snakk.Infrastructure.Realtime;

/// <summary>
/// HTTP-based implementation of IActivityBroadcaster for admin panel activity feed
/// Posts events to dedicated Snakk.Realtime microservice
/// </summary>
public class HttpActivityBroadcaster : IActivityBroadcaster
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpActivityBroadcaster> _logger;

    public HttpActivityBroadcaster(
        IHttpClientFactory httpClientFactory,
        ILogger<HttpActivityBroadcaster> logger)
    {
        _httpClient = httpClientFactory.CreateClient("ActivityBroadcaster");
        _logger = logger;
    }

    public async Task BroadcastPostCreated(string userId, string username, string postId, string discussionId, string discussionTitle, string? communityName, string? hubName, string? spaceName)
    {
        await BroadcastActivityAsync("post-created", new
        {
            userId,
            username,
            postId,
            discussionId,
            discussionTitle,
            communityName,
            hubName,
            spaceName,
            timestamp = DateTime.UtcNow
        });
    }

    public async Task BroadcastDiscussionCreated(string userId, string username, string discussionId, string discussionTitle, string? communityName, string? hubName, string? spaceName)
    {
        await BroadcastActivityAsync("discussion-created", new
        {
            userId,
            username,
            discussionId,
            discussionTitle,
            communityName,
            hubName,
            spaceName,
            timestamp = DateTime.UtcNow
        });
    }

    public async Task BroadcastReactionAdded(string userId, string username, string reactionType, string targetType, string targetId, string? targetTitle)
    {
        await BroadcastActivityAsync("reaction-added", new
        {
            userId,
            username,
            reactionType,
            targetType,
            targetId,
            targetTitle,
            timestamp = DateTime.UtcNow
        });
    }

    public async Task BroadcastFollowAdded(string userId, string username, string targetType, string targetId, string? targetName)
    {
        await BroadcastActivityAsync("follow-added", new
        {
            userId,
            username,
            targetType,
            targetId,
            targetName,
            timestamp = DateTime.UtcNow
        });
    }

    public async Task BroadcastUserRegistered(string userId, string username, string email)
    {
        await BroadcastActivityAsync("user-registered", new
        {
            userId,
            username,
            email,
            timestamp = DateTime.UtcNow
        });
    }

    public async Task BroadcastModerationAction(string moderatorId, string moderatorName, string action, string targetType, string? targetId, string? targetName, string? reason)
    {
        await BroadcastActivityAsync("moderation-action", new
        {
            moderatorId,
            moderatorName,
            action,
            targetType,
            targetId,
            targetName,
            reason,
            timestamp = DateTime.UtcNow
        });
    }

    public async Task BroadcastUserBanned(string moderatorId, string moderatorName, string userId, string username, string reason)
    {
        await BroadcastActivityAsync("user-banned", new
        {
            moderatorId,
            moderatorName,
            userId,
            username,
            reason,
            timestamp = DateTime.UtcNow
        });
    }

    public async Task BroadcastUserUnbanned(string moderatorId, string moderatorName, string userId, string username)
    {
        await BroadcastActivityAsync("user-unbanned", new
        {
            moderatorId,
            moderatorName,
            userId,
            username,
            timestamp = DateTime.UtcNow
        });
    }

    public async Task BroadcastContentDeleted(string moderatorId, string moderatorName, string contentType, string contentId, string? reason)
    {
        await BroadcastActivityAsync("content-deleted", new
        {
            moderatorId,
            moderatorName,
            contentType,
            contentId,
            reason,
            timestamp = DateTime.UtcNow
        });
    }

    private async Task BroadcastActivityAsync(string activityType, object data)
    {
        try
        {
            await _httpClient.PostAsJsonAsync("/api/broadcast/activity", new
            {
                ActivityType = activityType,
                TargetGroup = "admin-activity",  // Admin panel activity feed
                Data = data
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast activity: {ActivityType}", activityType);
        }
    }
}
