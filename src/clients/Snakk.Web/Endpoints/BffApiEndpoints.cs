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
        var result = await apiClient.GetNotificationsAsync(offset, pageSize);
        return result != null ? Results.Ok(result) : Results.Ok(new { items = Array.Empty<object>() });
    }

    private static async Task<IResult> GetUnreadNotificationCountAsync(SnakkApiClient apiClient)
    {
        var result = await apiClient.GetUnreadNotificationCountAsync();
        return Results.Ok(result);
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
        var result = await apiClient.GetSpaceFollowStatusAsync(spaceId);
        return Results.Ok(result);
    }

    private static async Task<IResult> ToggleSpaceFollowAsync(
        string spaceId,
        [FromQuery] string? level,
        SnakkApiClient apiClient)
    {
        var result = await apiClient.ToggleSpaceFollowAsync(spaceId, level);
        return Results.Ok(result);
    }

    private static async Task<IResult> SetSpaceFollowLevelAsync(
        string spaceId,
        [FromQuery] string level,
        SnakkApiClient apiClient)
    {
        var result = await apiClient.SetSpaceFollowLevelAsync(spaceId, level);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetDiscussionFollowStatusAsync(
        string discussionId,
        SnakkApiClient apiClient)
    {
        var result = await apiClient.GetDiscussionFollowStatusAsync(discussionId);
        return Results.Ok(result);
    }

    private static async Task<IResult> ToggleDiscussionFollowAsync(
        string discussionId,
        SnakkApiClient apiClient)
    {
        var result = await apiClient.ToggleDiscussionFollowAsync(discussionId);
        return Results.Ok(result);
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
        var result = await apiClient.GetPostReactionsAsync(postId);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetMyPostReactionAsync(
        string postId,
        SnakkApiClient apiClient)
    {
        var result = await apiClient.GetMyPostReactionAsync(postId);
        return Results.Ok(result);
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
        var result = await apiClient.PreviewMarkupAsync(request.Content);
        return Results.Ok(new { html = result });
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
        var result = await apiClient.GetFollowedSpacesAsync();
        return Results.Ok(new { items = result });
    }

    private static async Task<IResult> GetFollowedDiscussionsAsync(SnakkApiClient apiClient)
    {
        var result = await apiClient.GetFollowedDiscussionsAsync();
        return Results.Ok(new { items = result });
    }

    private static async Task<IResult> GetFollowedUsersAsync(SnakkApiClient apiClient)
    {
        var result = await apiClient.GetFollowedUsersAsync();
        return Results.Ok(new { items = result });
    }

    private static async Task<IResult> BatchUpdateReadStatesAsync(
        [FromBody] BatchUpdateReadStatesRequest request,
        SnakkApiClient apiClient)
    {
        await apiClient.BatchUpdateReadStatesAsync(request.Updates);
        return Results.Ok();
    }
}

public record ToggleReactionRequest(string Emoji);
public record PreviewMarkupRequest(string Content);
public record BffCreateReportRequest(string EntityType, string EntityId, string Reason, string? Description);
public record BatchUpdateReadStatesRequest(List<Services.ReadStateUpdateDto> Updates);
