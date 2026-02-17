namespace Snakk.Web.Endpoints;

using Microsoft.AspNetCore.Mvc;
using Snakk.Web.Services;

/// <summary>
/// Backend-for-Frontend API endpoints - aggregates and proxies API calls
/// JavaScript should call these instead of calling the Snakk.Api directly
/// </summary>
public static class BffApiEndpoints
{
    public static void MapBffApiEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/bff")
            .WithTags("BFF");

        // Homepage aggregated data
        group.MapGet("/homepage-data", GetHomepageDataAsync)
            .WithName("GetHomepageData");

        // Notifications
        group.MapGet("/notifications", GetNotificationsAsync)
            .WithName("BffGetNotifications");

        group.MapGet("/notifications/unread-count", GetUnreadNotificationCountAsync)
            .WithName("BffGetUnreadCount");

        group.MapPost("/notifications/{notificationId}/read", MarkNotificationAsReadAsync)
            .WithName("BffMarkNotificationRead");

        group.MapPost("/notifications/read-all", MarkAllNotificationsAsReadAsync)
            .WithName("BffMarkAllNotificationsRead");

        // Space follow actions
        group.MapGet("/spaces/{spaceId}/follow-status", GetSpaceFollowStatusAsync)
            .WithName("BffGetSpaceFollowStatus");

        group.MapPost("/spaces/{spaceId}/follow", ToggleSpaceFollowAsync)
            .WithName("BffToggleSpaceFollow");

        group.MapPut("/spaces/{spaceId}/follow-level", SetSpaceFollowLevelAsync)
            .WithName("BffSetSpaceFollowLevel");

        // Discussion follow actions
        group.MapGet("/discussions/{discussionId}/follow-status", GetDiscussionFollowStatusAsync)
            .WithName("BffGetDiscussionFollowStatus");

        group.MapPost("/discussions/{discussionId}/follow", ToggleDiscussionFollowAsync)
            .WithName("BffToggleDiscussionFollow");

        group.MapPost("/discussions/{discussionId}/mark-read", MarkDiscussionAsReadAsync)
            .WithName("BffMarkDiscussionRead");

        // Follow lists (for caching)
        group.MapGet("/follows/spaces", GetFollowedSpacesAsync)
            .WithName("BffGetFollowedSpaces");

        group.MapGet("/follows/discussions", GetFollowedDiscussionsAsync)
            .WithName("BffGetFollowedDiscussions");

        group.MapGet("/follows/users", GetFollowedUsersAsync)
            .WithName("BffGetFollowedUsers");

        // Batch read states
        group.MapPost("/read-states/batch", BatchUpdateReadStatesAsync)
            .WithName("BffBatchUpdateReadStates");

        // Post reactions
        group.MapGet("/posts/{postId}/reactions", GetPostReactionsAsync)
            .WithName("BffGetPostReactions");

        group.MapGet("/posts/{postId}/reactions/me", GetMyPostReactionAsync)
            .WithName("BffGetMyPostReaction");

        group.MapPost("/posts/{postId}/reactions", TogglePostReactionAsync)
            .WithName("BffTogglePostReaction");

        // Markup preview
        group.MapPost("/markup/preview", PreviewMarkupAsync)
            .WithName("BffPreviewMarkup");

        // Moderation
        group.MapPost("/moderation/reports", CreateReportAsync)
            .WithName("BffCreateReport");

        // Endless scroll data
        group.MapGet("/discussions/recent", GetRecentDiscussionsAsync)
            .WithName("BffGetRecentDiscussions");

        group.MapGet("/spaces/{spaceId}/discussions", GetSpaceDiscussionsAsync)
            .WithName("BffGetSpaceDiscussions");

        // Auth operations
        group.MapGet("/auth/status", GetAuthStatusAsync)
            .WithName("BffGetAuthStatus");

        group.MapPost("/auth/logout", LogoutAsync)
            .WithName("BffLogout");

        group.MapPost("/auth/refresh", RefreshTokenAsync)
            .WithName("BffRefreshToken")
            .AllowAnonymous(); // Refresh can happen before auth expires

        // User operations
        group.MapGet("/users/{userId}/stats", GetUserStatsAsync)
            .WithName("BffGetUserStats");

        group.MapGet("/users/{userId}/activity-history", GetUserActivityHistoryAsync)
            .WithName("BffGetUserActivityHistory");

        group.MapGet("/users/{userId}/follow-status", GetUserFollowStatusAsync)
            .WithName("BffGetUserFollowStatus");

        group.MapPost("/users/{userId}/follow", ToggleUserFollowAsync)
            .WithName("BffToggleUserFollow");

        // Search operations
        group.MapGet("/search/discussions", SearchDiscussionsAsync)
            .WithName("BffSearchDiscussions");

        group.MapGet("/search/posts", SearchPostsAsync)
            .WithName("BffSearchPosts");

        // Post operations
        group.MapGet("/discussions/{discussionId}/posts", GetDiscussionPostsAsync)
            .WithName("BffGetDiscussionPosts");

        group.MapPost("/posts/{postId}/edit", EditPostAsync)
            .WithName("BffEditPost");

        // Discussion preview
        group.MapGet("/discussions/{discussionId}/preview", GetDiscussionPreviewAsync)
            .WithName("BffGetDiscussionPreview");

        // Entity stats for popups
        group.MapGet("/hubs/{publicId}/stats", GetHubStatsForPopupAsync)
            .WithName("BffGetHubStats");

        group.MapGet("/spaces/{publicId}/stats", GetSpaceStatsForPopupAsync)
            .WithName("BffGetSpaceStats");

        group.MapGet("/communities/{publicId}/stats", GetCommunityStatsForPopupAsync)
            .WithName("BffGetCommunityStats");

        group.MapGet("/users/{publicId}/stats-popup", GetUserStatsForPopupAsync)
            .WithName("BffGetUserStatsPopup");

        group.MapGet("/discussions/{publicId}/stats", GetDiscussionStatsForPopupAsync)
            .WithName("BffGetDiscussionStats");

        // Moderation report reasons
        group.MapGet("/moderation/reports/reasons", GetModerationReportReasonsAsync)
            .WithName("BffGetModerationReportReasons");

        // Avatar proxy
        // Avatar endpoints removed - now handled by URL rewrite middleware in Program.cs
    }

    private static async Task<IResult> GetHomepageDataAsync(
        [FromQuery] string? communityId,
        [FromQuery] int offset,
        [FromQuery] int pageSize,
        SnakkApiClient apiClient)
    {
        // Aggregate multiple API calls into a single response
        var recentDiscussions = await apiClient.GetRecentDiscussionsAsync(offset, pageSize, communityId);
        var topActiveDiscussions = await apiClient.GetTopActiveDiscussionsAsync(communityId);
        var topActiveSpaces = await apiClient.GetTopActiveSpacesAsync(communityId);
        var topContributors = await apiClient.GetTopContributorsAsync(communityId);

        return Results.Ok(new
        {
            recentDiscussions,
            topActiveDiscussions,
            topActiveSpaces,
            topContributors
        });
    }

    private static async Task<IResult> GetNotificationsAsync(
        [FromQuery] int offset,
        [FromQuery] int pageSize,
        SnakkApiClient apiClient)
    {
        var apiResult = await apiClient.GetNotificationsAsync(offset, pageSize);
        if (apiResult?.Items == null)
        {
            return Results.Ok(new Models.Bff.BffNotificationsResponse
            {
                Items = new List<Models.Bff.BffNotificationResponse>()
            });
        }

        // Map API DTOs → BFF DTOs
        var bffResponse = new Models.Bff.BffNotificationsResponse
        {
            Items = apiResult.Items.Select(n => new Models.Bff.BffNotificationResponse
            {
                PublicId = n.PublicId,
                Type = n.Type,
                Title = n.Title,
                Body = n.Body,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt.ToString("O"), // ISO 8601 format
                SourceDiscussionId = n.SourceDiscussionId
            }).ToList()
        };

        return Results.Ok(bffResponse);
    }

    private static async Task<IResult> GetUnreadNotificationCountAsync(SnakkApiClient apiClient)
    {
        var apiResult = await apiClient.GetUnreadNotificationCountAsync();

        // Map API DTO → BFF DTO
        var bffResponse = new Models.Bff.BffNotificationCountResponse
        {
            Count = apiResult?.Count ?? 0
        };

        return Results.Ok(bffResponse);
    }

    private static async Task<IResult> MarkNotificationAsReadAsync(
        string notificationId,
        SnakkApiClient apiClient)
    {
        await apiClient.MarkNotificationAsReadAsync(notificationId);
        return Results.Ok();
    }

    private static async Task<IResult> MarkAllNotificationsAsReadAsync(SnakkApiClient apiClient)
    {
        await apiClient.MarkAllNotificationsAsReadAsync();
        return Results.Ok();
    }

    private static async Task<IResult> GetSpaceFollowStatusAsync(
        string spaceId,
        SnakkApiClient apiClient)
    {
        var apiResult = await apiClient.GetSpaceFollowStatusAsync(spaceId);
        if (apiResult == null) return Results.NotFound();

        var bffResponse = new Models.Bff.BffFollowStatusResponse
        {
            IsFollowing = apiResult.IsFollowing,
            Level = apiResult.Level
        };

        return Results.Ok(bffResponse);
    }

    private static async Task<IResult> ToggleSpaceFollowAsync(
        string spaceId,
        [FromQuery] string? level,
        SnakkApiClient apiClient)
    {
        var apiResult = await apiClient.ToggleSpaceFollowAsync(spaceId, level);
        if (apiResult == null) return Results.BadRequest();

        var bffResponse = new Models.Bff.BffFollowResultResponse
        {
            IsFollowing = apiResult.IsFollowing,
            Level = apiResult.Level
        };

        return Results.Ok(bffResponse);
    }

    private static async Task<IResult> SetSpaceFollowLevelAsync(
        string spaceId,
        [FromQuery] string level,
        SnakkApiClient apiClient)
    {
        var apiResult = await apiClient.SetSpaceFollowLevelAsync(spaceId, level);
        if (apiResult == null) return Results.BadRequest();

        var bffResponse = new Models.Bff.BffFollowResultResponse
        {
            IsFollowing = apiResult.IsFollowing,
            Level = apiResult.Level
        };

        return Results.Ok(bffResponse);
    }

    private static async Task<IResult> GetDiscussionFollowStatusAsync(
        string discussionId,
        SnakkApiClient apiClient)
    {
        var apiResult = await apiClient.GetDiscussionFollowStatusAsync(discussionId);
        if (apiResult == null) return Results.NotFound();

        var bffResponse = new Models.Bff.BffFollowStatusResponse
        {
            IsFollowing = apiResult.IsFollowing,
            Level = apiResult.Level
        };

        return Results.Ok(bffResponse);
    }

    private static async Task<IResult> ToggleDiscussionFollowAsync(
        string discussionId,
        SnakkApiClient apiClient)
    {
        var apiResult = await apiClient.ToggleDiscussionFollowAsync(discussionId);
        if (apiResult == null) return Results.BadRequest();

        var bffResponse = new Models.Bff.BffFollowResultResponse
        {
            IsFollowing = apiResult.IsFollowing,
            Level = apiResult.Level
        };

        return Results.Ok(bffResponse);
    }

    private static async Task<IResult> MarkDiscussionAsReadAsync(
        string discussionId,
        [FromQuery] string userId,
        [FromQuery] string postId,
        SnakkApiClient apiClient)
    {
        await apiClient.MarkDiscussionAsReadAsync(discussionId, userId, postId);
        return Results.Ok();
    }

    private static async Task<IResult> GetPostReactionsAsync(
        string postId,
        SnakkApiClient apiClient)
    {
        var apiResult = await apiClient.GetPostReactionsAsync(postId);

        var bffResponse = new Models.Bff.BffReactionsResponse
        {
            Reactions = apiResult ?? new Dictionary<string, int>()
        };

        return Results.Ok(bffResponse);
    }

    private static async Task<IResult> GetMyPostReactionAsync(
        string postId,
        SnakkApiClient apiClient)
    {
        var apiResult = await apiClient.GetMyPostReactionAsync(postId);

        var bffResponse = new Models.Bff.BffMyReactionResponse
        {
            Emoji = apiResult?.Emoji
        };

        return Results.Ok(bffResponse);
    }

    private static async Task<IResult> TogglePostReactionAsync(
        string postId,
        [FromBody] ToggleReactionRequest request,
        SnakkApiClient apiClient)
    {
        await apiClient.TogglePostReactionAsync(postId, request.Emoji);
        return Results.Ok();
    }

    private static async Task<IResult> PreviewMarkupAsync(
        [FromBody] PreviewMarkupRequest request,
        SnakkApiClient apiClient)
    {
        var html = await apiClient.PreviewMarkupAsync(request.Content);

        var bffResponse = new Models.Bff.BffMarkupPreviewResponse
        {
            Html = html ?? string.Empty
        };

        return Results.Ok(bffResponse);
    }

    private static async Task<IResult> CreateReportAsync(
        [FromBody] BffCreateReportRequest request,
        SnakkApiClient apiClient)
    {
        var apiRequest = new Models.CreateReportRequest(
            request.EntityType,
            request.EntityId,
            request.Reason,
            request.Description,
            null); // Details
        await apiClient.CreateReportAsync(apiRequest);
        return Results.Ok();
    }

    private static async Task<IResult> GetRecentDiscussionsAsync(
        [FromQuery] int offset,
        [FromQuery] int pageSize,
        [FromQuery] string? communityId,
        [FromQuery] string? cursor,
        SnakkApiClient apiClient)
    {
        var result = await apiClient.GetRecentDiscussionsAsync(offset, pageSize, communityId, cursor);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetSpaceDiscussionsAsync(
        string spaceId,
        [FromQuery] int offset,
        [FromQuery] int pageSize,
        SnakkApiClient apiClient)
    {
        var result = await apiClient.GetSpaceDiscussionsAsync(spaceId, offset, pageSize);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetFollowedSpacesAsync(SnakkApiClient apiClient)
    {
        var apiResult = await apiClient.GetFollowedSpacesAsync();

        var bffResponse = new Models.Bff.BffFollowedEntitiesResponse
        {
            Items = apiResult
        };

        return Results.Ok(bffResponse);
    }

    private static async Task<IResult> GetFollowedDiscussionsAsync(SnakkApiClient apiClient)
    {
        var apiResult = await apiClient.GetFollowedDiscussionsAsync();

        var bffResponse = new Models.Bff.BffFollowedEntitiesResponse
        {
            Items = apiResult
        };

        return Results.Ok(bffResponse);
    }

    private static async Task<IResult> GetFollowedUsersAsync(SnakkApiClient apiClient)
    {
        var apiResult = await apiClient.GetFollowedUsersAsync();

        var bffResponse = new Models.Bff.BffFollowedEntitiesResponse
        {
            Items = apiResult
        };

        return Results.Ok(bffResponse);
    }

    private static async Task<IResult> BatchUpdateReadStatesAsync(
        [FromBody] BatchUpdateReadStatesRequest request,
        SnakkApiClient apiClient)
    {
        await apiClient.BatchUpdateReadStatesAsync(request.Updates);
        return Results.Ok();
    }

    // Auth endpoints
    private static async Task<IResult> GetAuthStatusAsync(SnakkApiClient apiClient, HttpContext httpContext)
    {
        var apiResult = await apiClient.GetAuthStatusAsync();
        if (apiResult == null) return Results.Unauthorized();

        // Map API DTO → BFF DTO (decouples frontend from API structure)
        var bffResponse = new Models.Bff.BffAuthStatusResponse
        {
            IsAuthenticated = apiResult.IsAuthenticated,
            PublicId = apiResult.PublicId,
            DisplayName = apiResult.DisplayName,
            EmailVerified = apiResult.EmailVerified,
            Role = apiResult.Role,
            AvatarUrl = apiResult.AvatarUrl
        };

        // Prevent browser caching of auth status (critical for logout to work correctly)
        httpContext.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate, max-age=0";
        httpContext.Response.Headers.Pragma = "no-cache";
        httpContext.Response.Headers.Expires = "0";

        return Results.Ok(bffResponse);
    }

    private static async Task<IResult> LogoutAsync(SnakkApiClient apiClient)
    {
        await apiClient.LogoutAsync();
        return Results.Ok();
    }

    private static async Task<IResult> RefreshTokenAsync(
        [FromBody] RefreshTokenRequestDto request,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        try
        {
            var httpClient = httpClientFactory.CreateClient();
            var apiBaseUrl = configuration["ApiBaseUrl"] ?? "http://localhost:5000";

            var requestBody = new
            {
                refreshToken = request.RefreshToken
            };

            var content = new StringContent(
                System.Text.Json.JsonSerializer.Serialize(requestBody),
                System.Text.Encoding.UTF8,
                "application/json");

            var response = await httpClient.PostAsync($"{apiBaseUrl}/auth/refresh", content);

            if (!response.IsSuccessStatusCode)
                return Results.Unauthorized();

            var responseBody = await response.Content.ReadAsStringAsync();
            var result = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(responseBody);

            if (!result.TryGetProperty("accessToken", out var accessToken) ||
                !result.TryGetProperty("refreshToken", out var refreshToken))
            {
                return Results.Unauthorized();
            }

            return Results.Ok(new
            {
                accessToken = accessToken.GetString(),
                refreshToken = refreshToken.GetString()
            });
        }
        catch
        {
            return Results.Unauthorized();
        }
    }

    // User endpoints
    private static async Task<IResult> GetUserStatsAsync(
        string userId,
        SnakkApiClient apiClient)
    {
        var apiResult = await apiClient.GetUserStatsAsync(userId);
        if (apiResult == null) return Results.NotFound();

        var bffResponse = new Models.Bff.BffUserStatsResponse
        {
            PublicId = apiResult.PublicId,
            DisplayName = apiResult.DisplayName,
            AvatarUrl = apiResult.AvatarUrl,
            DiscussionCount = apiResult.DiscussionCount,
            ReplyCount = apiResult.ReplyCount,
            FollowerCount = apiResult.FollowerCount,
            FollowingCount = apiResult.FollowingCount
        };

        return Results.Ok(bffResponse);
    }

    private static async Task<IResult> GetUserActivityHistoryAsync(
        string userId,
        [FromQuery] int days,
        SnakkApiClient apiClient)
    {
        var apiResult = await apiClient.GetUserActivityHistoryAsync(userId, days);
        if (apiResult == null) return Results.NotFound();

        var bffResponse = new Models.Bff.BffUserActivityResponse
        {
            Activities = apiResult.Activities.Select(a => new Models.Bff.BffDailyActivityResponse
            {
                Date = a.Date,
                PostCount = a.PostCount,
                DiscussionCount = a.DiscussionCount
            }).ToList()
        };

        return Results.Ok(bffResponse);
    }

    private static async Task<IResult> GetUserFollowStatusAsync(
        string userId,
        [FromQuery] string currentUserId,
        SnakkApiClient apiClient)
    {
        var apiResult = await apiClient.GetUserFollowStatusAsync(userId, currentUserId);
        if (apiResult == null) return Results.NotFound();

        var bffResponse = new Models.Bff.BffUserFollowStatusResponse
        {
            IsFollowing = apiResult.IsFollowing
        };

        return Results.Ok(bffResponse);
    }

    private static async Task<IResult> ToggleUserFollowAsync(
        string userId,
        SnakkApiClient apiClient)
    {
        var apiResult = await apiClient.ToggleUserFollowAsync(userId);
        if (apiResult == null) return Results.BadRequest();

        var bffResponse = new Models.Bff.BffFollowResultResponse
        {
            IsFollowing = apiResult.IsFollowing,
            Level = apiResult.Level
        };

        return Results.Ok(bffResponse);
    }

    // Search endpoints
    private static async Task<IResult> SearchDiscussionsAsync(
        [FromQuery] string? authorPublicId,
        [FromQuery] int pageSize,
        SnakkApiClient apiClient)
    {
        var result = await apiClient.SearchDiscussionsAsync(
            query: null,
            authorPublicId: authorPublicId,
            spacePublicId: null,
            hubPublicId: null,
            offset: 0,
            pageSize: pageSize);
        return result != null ? Results.Ok(result) : Results.Ok(new { items = Array.Empty<object>() });
    }

    private static async Task<IResult> SearchPostsAsync(
        [FromQuery] string? authorPublicId,
        [FromQuery] int pageSize,
        SnakkApiClient apiClient)
    {
        var result = await apiClient.SearchPostsAsync(
            query: null,
            authorPublicId: authorPublicId,
            discussionPublicId: null,
            spacePublicId: null,
            offset: 0,
            pageSize: pageSize);
        return result != null ? Results.Ok(result) : Results.Ok(new { items = Array.Empty<object>() });
    }

    // Post endpoints
    private static async Task<IResult> GetDiscussionPostsAsync(
        string discussionId,
        [FromQuery] int offset,
        [FromQuery] int pageSize,
        SnakkApiClient apiClient)
    {
        var result = await apiClient.GetDiscussionPostsAsync(discussionId, offset, pageSize);
        return result != null ? Results.Ok(result) : Results.NotFound();
    }

    private static async Task<IResult> EditPostAsync(
        string postId,
        [FromQuery] string userId,
        [FromQuery] string content,
        SnakkApiClient apiClient)
    {
        var success = await apiClient.EditPostAsync(postId, userId, content);
        return success ? Results.Ok() : Results.BadRequest();
    }

    // Discussion preview endpoint
    private static async Task<IResult> GetDiscussionPreviewAsync(
        string discussionId,
        SnakkApiClient apiClient)
    {
        var apiResult = await apiClient.GetDiscussionPreviewAsync(discussionId);
        if (apiResult == null) return Results.NotFound();

        var bffResponse = new Models.Bff.BffDiscussionPreviewResponse
        {
            Content = apiResult.Content
        };

        return Results.Ok(bffResponse);
    }

    // Entity stats for popups
    private static async Task<IResult> GetHubStatsForPopupAsync(
        string publicId,
        SnakkApiClient apiClient)
    {
        var apiResult = await apiClient.GetHubStatsAsync(publicId);
        if (apiResult == null) return Results.NotFound();

        // Map API DTO → BFF DTO
        var bffResponse = new Models.Bff.BffHubStatsResponse
        {
            PublicId = apiResult.PublicId,
            Name = apiResult.Name,
            Description = apiResult.Description,
            AvatarUrl = apiResult.AvatarUrl,
            SpaceCount = apiResult.SpaceCount,
            DiscussionCount = apiResult.DiscussionCount,
            ReplyCount = apiResult.ReplyCount
        };

        return Results.Ok(bffResponse);
    }

    private static async Task<IResult> GetSpaceStatsForPopupAsync(
        string publicId,
        SnakkApiClient apiClient)
    {
        var apiResult = await apiClient.GetSpaceStatsAsync(publicId);
        if (apiResult == null) return Results.NotFound();

        // Map API DTO → BFF DTO
        var bffResponse = new Models.Bff.BffSpaceStatsResponse
        {
            PublicId = apiResult.PublicId,
            Name = apiResult.Name,
            Description = apiResult.Description,
            AvatarUrl = apiResult.AvatarUrl,
            DiscussionCount = apiResult.DiscussionCount,
            ReplyCount = apiResult.ReplyCount,
            FollowerCount = apiResult.FollowerCount
        };

        return Results.Ok(bffResponse);
    }

    private static async Task<IResult> GetCommunityStatsForPopupAsync(
        string publicId,
        SnakkApiClient apiClient)
    {
        var apiResult = await apiClient.GetCommunityStatsAsync(publicId);
        if (apiResult == null) return Results.NotFound();

        // Map API DTO → BFF DTO
        var bffResponse = new Models.Bff.BffCommunityStatsResponse
        {
            PublicId = apiResult.PublicId,
            Name = apiResult.Name,
            Description = apiResult.Description,
            AvatarUrl = apiResult.AvatarUrl,
            HubCount = apiResult.HubCount,
            SpaceCount = apiResult.SpaceCount,
            DiscussionCount = apiResult.DiscussionCount,
            ReplyCount = apiResult.ReplyCount
        };

        return Results.Ok(bffResponse);
    }

    private static async Task<IResult> GetUserStatsForPopupAsync(
        string publicId,
        SnakkApiClient apiClient)
    {
        var apiResult = await apiClient.GetUserStatsAsync(publicId);
        if (apiResult == null) return Results.NotFound();

        var bffResponse = new Models.Bff.BffUserStatsResponse
        {
            PublicId = apiResult.PublicId,
            DisplayName = apiResult.DisplayName,
            AvatarUrl = apiResult.AvatarUrl,
            DiscussionCount = apiResult.DiscussionCount,
            ReplyCount = apiResult.ReplyCount,
            FollowerCount = apiResult.FollowerCount,
            FollowingCount = apiResult.FollowingCount
        };

        return Results.Ok(bffResponse);
    }

    private static async Task<IResult> GetDiscussionStatsForPopupAsync(
        string publicId,
        SnakkApiClient apiClient)
    {
        var result = await apiClient.GetDiscussionStatsForPopupAsync(publicId);
        return result != null ? Results.Ok(result) : Results.NotFound();
    }

    // Moderation report reasons
    private static async Task<IResult> GetModerationReportReasonsAsync(
        [FromQuery] string? reportType,
        SnakkApiClient apiClient)
    {
        var result = await apiClient.GetReportReasonsAsync();
        return result != null ? Results.Ok(result) : Results.Ok(Array.Empty<object>());
    }

    // Avatar proxy endpoints
    // Avatar endpoints removed - now handled by URL rewrite middleware in Program.cs
}

public record ToggleReactionRequest(string Emoji);
public record RefreshTokenRequestDto(string RefreshToken);
public record PreviewMarkupRequest(string Content);
public record BffCreateReportRequest(string EntityType, string EntityId, string Reason, string? Description);
public record BatchUpdateReadStatesRequest(List<Services.ReadStateUpdateDto> Updates);
