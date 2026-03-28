namespace Snakk.Web.Endpoints;

using Microsoft.AspNetCore.Mvc;
using Snakk.Web.Helpers;
using Snakk.Web.Models.Bff;
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

        // Poll voting
        group.MapGet("/discussions/{discussionId}/poll", GetPollAsync)
            .WithName("BffGetPoll");
        group.MapPost("/discussions/{discussionId}/poll/vote", VotePollAsync)
            .WithName("BffVotePoll");
        group.MapDelete("/discussions/{discussionId}/poll/vote", RemovePollVoteAsync)
            .WithName("BffRemovePollVote");

        // Question
        group.MapGet("/discussions/{discussionId}/question", GetQuestionStatusBffAsync)
            .WithName("BffGetQuestionStatus");
        group.MapPost("/discussions/{discussionId}/question/solve", MarkQuestionSolvedBffAsync)
            .WithName("BffMarkQuestionSolved");

        // Debate
        group.MapGet("/discussions/{discussionId}/debate", GetDebateInfoBffAsync)
            .WithName("BffGetDebateInfo");

        group.MapPost("/discussions/{discussionId}/debate/position", SetPostDebatePositionBffAsync)
            .WithName("BffSetPostDebatePosition");

        // Link
        group.MapGet("/discussions/{discussionId}/link", GetDiscussionLinkBffAsync)
            .WithName("BffGetDiscussionLink");

        // Journal
        group.MapGet("/discussions/{discussionId}/journal", GetJournalEntriesBffAsync)
            .WithName("BffGetJournalEntries");
        group.MapPost("/discussions/{discussionId}/journal/entry", AddJournalEntryBffAsync)
            .WithName("BffAddJournalEntry");

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

        group.MapGet("/posts/{postId}/reactions/me", GetMyPostReactionsAsync)
            .WithName("BffGetMyPostReactions");

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

        // Current user (me) operations
        group.MapGet("/me", GetCurrentUserMeAsync)
            .WithName("BffGetCurrentUser");

        group.MapPut("/me/profile", UpdateProfileMeAsync)
            .WithName("BffUpdateProfileMe");

        group.MapPut("/me/preferences", UpdatePreferencesMeAsync)
            .WithName("BffUpdatePreferences");

        group.MapGet("/me/devices", GetMyDevicesAsync)
            .WithName("BffGetMyDevices");

        group.MapDelete("/me/devices/{deviceId}", RevokeMyDeviceAsync)
            .WithName("BffRevokeMyDevice");

        // User operations
        group.MapGet("/users/{userId}/stats", GetUserStatsAsync)
            .WithName("BffGetUserStats");

        group.MapGet("/users/{userId}/activity-history", GetUserActivityHistoryAsync)
            .WithName("BffGetUserActivityHistory");

        group.MapGet("/users/{userId}/follow-status", GetUserFollowStatusAsync)
            .WithName("BffGetUserFollowStatus");

        group.MapPost("/users/{userId}/follow", ToggleUserFollowAsync)
            .WithName("BffToggleUserFollow");

        group.MapGet("/me/display-name-history", GetDisplayNameHistoryAsync)
            .WithName("BffGetDisplayNameHistory");

        group.MapPost("/me/feed-token", GenerateFeedTokenAsync)
            .WithName("BffGenerateFeedToken");
        group.MapDelete("/me/feed-token", RevokeFeedTokenAsync)
            .WithName("BffRevokeFeedToken");

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

        group.MapDelete("/posts/{postId}", DeletePostAsync)
            .WithName("BffDeletePost");

        group.MapGet("/posts/{postId}/history", GetPostHistoryAsync)
            .WithName("BffGetPostHistory");

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

        // Media upload + delete
        group.MapPost("/media/upload", UploadMediaAsync)
            .WithName("BffUploadMedia")
            .DisableAntiforgery();
        group.MapDelete("/media/draft", DeleteDraftMediaBffAsync)
            .WithName("BffDeleteDraftMedia");

        // Avatar upload + delete (proxy to internal API)
        group.MapPost("/avatars/upload", UploadAvatarBffAsync)
            .WithName("BffUploadAvatar")
            .DisableAntiforgery();
        group.MapDelete("/avatars", DeleteAvatarBffAsync)
            .WithName("BffDeleteAvatar");
    }

    private static async Task<IResult> GetHomepageDataAsync(
        [FromQuery] string? communityId,
        [FromQuery] int offset,
        [FromQuery] int pageSize,
        SnakkApiClient apiClient)
    {
        // Aggregate multiple API calls in parallel
        var recentTask = apiClient.GetRecentDiscussionsAsync(offset, pageSize, communityId);
        var topActiveTask = apiClient.GetTopActiveDiscussionsAsync(communityId);
        var topSpacesTask = apiClient.GetTopActiveSpacesAsync(communityId);
        var topContributorsTask = apiClient.GetTopContributorsAsync(communityId);

        await Task.WhenAll(recentTask, topActiveTask, topSpacesTask, topContributorsTask);

        return Results.Ok(new
        {
            recentDiscussions = recentTask.Result,
            topActiveDiscussions = topActiveTask.Result,
            topActiveSpaces = topSpacesTask.Result,
            topContributors = topContributorsTask.Result
        });
    }

    private static async Task<IResult> GetNotificationsAsync(
        [FromQuery] int offset,
        [FromQuery] int pageSize,
        SnakkApiClient apiClient)
    {
        var apiResult = await apiClient.GetNotificationsAsync(offset, pageSize);
        if (apiResult?.Items is null)
        {
            return Results.Ok(new Models.Bff.BffNotificationsResponse
            {
                Items = []
            });
        }

        // Map API DTOs → BFF DTOs
        var bffResponse = new Models.Bff.BffNotificationsResponse
        {
            Items = apiResult.Items.Select(n => new Models.Bff.BffNotificationResponse
            {
                PublicId = n.PublicId,
                Type = n.Type,
                Title = n.Message,
                Body = string.Empty,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt.ToDateTime().ToString("O"), // ISO 8601 format
                SourceDiscussionId = n.TargetPublicId
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
        if (apiResult is null) return Results.NotFound();

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
        var result = await apiClient.ToggleSpaceFollowResultAsync(spaceId, level);

        if (!result.IsSuccess)
            return MapGrpcError(result.Status, result.Error);

        var bffResponse = new Models.Bff.BffFollowResultResponse
        {
            IsFollowing = result.Value!.IsFollowing,
            Level = result.Value.Level
        };

        return Results.Ok(bffResponse);
    }

    private static async Task<IResult> SetSpaceFollowLevelAsync(
        string spaceId,
        [FromQuery] string level,
        SnakkApiClient apiClient)
    {
        var apiResult = await apiClient.SetSpaceFollowLevelAsync(spaceId, level);
        if (apiResult is null) return Results.BadRequest();

        var bffResponse = new Models.Bff.BffFollowResultResponse
        {
            IsFollowing = true, // Setting level implies following
            Level = apiResult.Level
        };

        return Results.Ok(bffResponse);
    }

    private static async Task<IResult> GetDiscussionFollowStatusAsync(
        string discussionId,
        SnakkApiClient apiClient)
    {
        var apiResult = await apiClient.GetDiscussionFollowStatusAsync(discussionId);
        if (apiResult is null) return Results.NotFound();

        var bffResponse = new Models.Bff.BffFollowStatusResponse
        {
            IsFollowing = apiResult.IsFollowing,
            Level = null // Discussion follows don't have levels
        };

        return Results.Ok(bffResponse);
    }

    private static async Task<IResult> ToggleDiscussionFollowAsync(
        string discussionId,
        SnakkApiClient apiClient)
    {
        var result = await apiClient.ToggleDiscussionFollowResultAsync(discussionId);

        if (!result.IsSuccess)
            return MapGrpcError(result.Status, result.Error);

        var bffResponse = new Models.Bff.BffFollowResultResponse
        {
            IsFollowing = result.Value!.IsFollowing,
            Level = null // Discussion follows don't have levels
        };

        return Results.Ok(bffResponse);
    }

    private static async Task<IResult> MarkDiscussionAsReadAsync(
        string discussionId,
        [FromQuery] string postId,
        HttpContext httpContext,
        SnakkApiClient apiClient)
    {
        // Read userId from auth claims, not query params (IDOR prevention)
        var userId = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "";
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
            Counts = apiResult ?? new Dictionary<string, int>()
        };

        return Results.Ok(bffResponse);
    }

    private static async Task<IResult> GetMyPostReactionsAsync(
        string postId,
        SnakkApiClient apiClient)
    {
        var apiResult = await apiClient.GetMyPostReactionsAsync(postId);

        var bffResponse = new Models.Bff.BffMyReactionsResponse
        {
            Reactions = apiResult ?? []
        };

        return Results.Ok(bffResponse);
    }

    private static async Task<IResult> TogglePostReactionAsync(
        string postId,
        [FromBody] ToggleReactionRequest request,
        SnakkApiClient apiClient)
    {
        await apiClient.TogglePostReactionAsync(postId, request.Type);
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
        SnakkApiClient apiClient)
    {
        var result = await apiClient.GetRecentDiscussionsAsync(offset, pageSize, communityId);
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
        await apiClient.BatchUpdateReadStatesAsync(request.Updates.Select(u => (u.DiscussionId, u.PostId)).ToList());
        return Results.Ok();
    }

    // Auth endpoints
    private static async Task<IResult> GetAuthStatusAsync(SnakkApiClient apiClient, HttpContext httpContext)
    {
        var apiResult = await apiClient.GetAuthStatusAsync();
        if (apiResult is null) return Results.Unauthorized();

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

    private static async Task<IResult> LogoutAsync(SnakkApiClient apiClient, HttpContext httpContext)
    {
        await apiClient.LogoutAsync();
        AuthCookieHelper.DeleteAuthCookies(httpContext);

        return Results.Ok();
    }

    private static async Task<IResult> RefreshTokenAsync(
        HttpContext httpContext,
        Snakk.Protos.Auth.AuthService.AuthServiceClient authClient)
    {
        try
        {
            // Read refresh token from cookie (not from request body)
            var currentRefreshToken = httpContext.Request.Cookies[AuthCookieHelper.RefreshCookieName];
            if (string.IsNullOrEmpty(currentRefreshToken))
                return Results.Unauthorized();

            var response = await authClient.RefreshTokenAsync(
                new Snakk.Protos.Auth.RefreshTokenRequest { RefreshToken = currentRefreshToken });

            if (string.IsNullOrEmpty(response.AccessToken) || string.IsNullOrEmpty(response.RefreshToken))
                return Results.Unauthorized();

            // Set updated cookies (no tokens in response body)
            AuthCookieHelper.SetAuthCookies(httpContext, response.AccessToken, response.RefreshToken);
            return Results.Ok();
        }
        catch
        {
            return Results.Unauthorized();
        }
    }

    /// <summary>
    /// Refreshes the auth cookies by requesting a new JWT via the refresh token.
    /// Used after operations that change JWT claims (e.g. avatar upload/delete).
    /// </summary>
    private static async Task RefreshAuthCookiesAsync(
        HttpContext httpContext,
        Snakk.Protos.Auth.AuthService.AuthServiceClient authClient)
    {
        try
        {
            var refreshToken = httpContext.Request.Cookies[AuthCookieHelper.RefreshCookieName];
            if (string.IsNullOrEmpty(refreshToken)) return;

            var response = await authClient.RefreshTokenAsync(
                new Snakk.Protos.Auth.RefreshTokenRequest { RefreshToken = refreshToken });

            if (!string.IsNullOrEmpty(response.AccessToken) && !string.IsNullOrEmpty(response.RefreshToken))
                AuthCookieHelper.SetAuthCookies(httpContext, response.AccessToken, response.RefreshToken);
        }
        catch { /* best-effort — avatar was saved, cookie refresh is non-critical */ }
    }

    // Current user (me) endpoints
    private static async Task<IResult> GetCurrentUserMeAsync(SnakkApiClient apiClient, HttpContext httpContext)
    {
        var apiResult = await apiClient.GetCurrentUserAsync();
        if (apiResult is null) return Results.Unauthorized();

        // Sync timezone cookie for server-side rendering
        var timezone = apiResult.HasTimezone ? apiResult.Timezone : null;
        AuthCookieHelper.SetTimezoneCookie(httpContext, timezone);

        return Results.Ok(new
        {
            publicId = apiResult.PublicId,
            displayName = apiResult.DisplayName,
            email = apiResult.Email,
            emailVerified = apiResult.EmailVerified,
            oAuthProvider = apiResult.OauthProvider,
            autoFollowOnReply = apiResult.AutoFollowOnReply,
            timezone = timezone,
            displayNameChangedAt = apiResult.DisplayNameChangedAt != null
                ? apiResult.DisplayNameChangedAt.ToDateTime().ToString("o")
                : null,
            isDisplayNameLocked = apiResult.IsDisplayNameLocked,
            hasPassword = apiResult.HasPassword,
            avatarUrl = apiResult.HasAvatarUrl ? apiResult.AvatarUrl : null,
            bio = apiResult.HasBio ? apiResult.Bio : null,
            feedToken = apiResult.HasFeedToken ? apiResult.FeedToken : null
        });
    }

    private static async Task<IResult> GetDisplayNameHistoryAsync(SnakkApiClient apiClient)
    {
        var result = await apiClient.GetDisplayNameHistoryAsync();
        if (result is null)
            return Results.Ok(new { entries = Array.Empty<object>() });

        var entries = result.Entries.Select(e => new
        {
            previousName = e.PreviousName,
            newName = e.NewName,
            changedAt = e.ChangedAt?.ToDateTime().ToString("o") ?? ""
        }).ToList();

        return Results.Ok(new { entries });
    }

    private static async Task<IResult> UpdateProfileMeAsync(
        [FromBody] UpdateProfileRequestDto request,
        SnakkApiClient apiClient)
    {
        var result = await apiClient.UpdateProfileAsync(request.DisplayName, request.Password, request.TurnstileToken);

        if (result is null)
            return Results.StatusCode(503);

        if (!result.Success)
            return Results.BadRequest(new { error = result.Message });

        return Results.Ok(new
        {
            message = result.Message,
            token = result.HasToken ? result.Token : null
        });
    }

    private static async Task<IResult> UpdatePreferencesMeAsync(
        [FromBody] UpdatePreferencesRequestDto request,
        SnakkApiClient apiClient,
        HttpContext httpContext)
    {
        var success = await apiClient.UpdatePreferencesAsync(request.AutoFollowOnReply, request.Timezone, request.Bio);
        if (!success) return Results.BadRequest(new { error = "Failed to update preferences" });

        // Update timezone cookie
        if (request.Timezone is not null)
            AuthCookieHelper.SetTimezoneCookie(httpContext, string.IsNullOrEmpty(request.Timezone) ? null : request.Timezone);

        return Results.Ok();
    }

    // User endpoints
    private static async Task<IResult> GetUserStatsAsync(
        string userId,
        SnakkApiClient apiClient)
    {
        var apiResult = await apiClient.GetUserStatsAsync(userId);
        if (apiResult is null) return Results.NotFound();

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
        if (apiResult is null) return Results.NotFound();

        var bffResponse = new Models.Bff.BffUserActivityResponse
        {
            Activities = apiResult.Data.Select(a => new Models.Bff.BffDailyActivityResponse
            {
                Date = a.Date,
                PostCount = a.Posts,
                DiscussionCount = a.Discussions
            }).ToList()
        };

        return Results.Ok(bffResponse);
    }

    private static async Task<IResult> GetUserFollowStatusAsync(
        string userId,
        HttpContext httpContext,
        SnakkApiClient apiClient)
    {
        // Read currentUserId from auth claims, not query params (IDOR prevention)
        var currentUserId = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "";
        var apiResult = await apiClient.GetUserFollowStatusAsync(userId, currentUserId);
        if (apiResult is null) return Results.NotFound();

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
        var result = await apiClient.ToggleUserFollowResultAsync(userId);

        if (!result.IsSuccess)
            return MapGrpcError(result.Status, result.Error);

        var bffResponse = new Models.Bff.BffFollowResultResponse
        {
            IsFollowing = result.Value!.IsFollowing,
            Level = null // User follows don't have levels
        };

        return Results.Ok(bffResponse);
    }

    // Search endpoints
    private static async Task<IResult> SearchDiscussionsAsync(
        SnakkApiClient apiClient,
        ICommunityContext communityContext,
        [FromQuery] string? q = null,
        [FromQuery] string? authorPublicId = null,
        [FromQuery] int pageSize = 20,
        [FromQuery] int offset = 0)
    {
        var result = await apiClient.SearchDiscussionsAsync(
            query: q,
            authorPublicId: authorPublicId,
            spacePublicId: null,
            hubPublicId: null,
            offset: offset,
            pageSize: pageSize);

        if (result is null)
            return Results.Ok(new BffSearchResponse<BffDiscussionSearchItem> { Items = [], Offset = 0, PageSize = pageSize, HasMoreItems = false });

        var items = result.Items.Select(d =>
        {
            var slugId = SnakkUrlHelper.DiscussionSlugId(d.Slug, d.PublicId);
            var url = SnakkUrlHelper.Discussion(d.CommunitySlug, communityContext, d.Hub.Slug, d.Space.Slug, slugId);
            return new BffDiscussionSearchItem
            {
                Url = url,
                Title = d.Title,
                HubName = d.Hub.Name,
                SpaceName = d.Space.Name,
                PostCount = d.PostCount,
                ReactionCount = d.ReactionCount,
                CreatedAt = d.CreatedAt?.ToDateTime().ToString("o") ?? "",
                LastActivityAt = d.LastActivityAt?.ToDateTime().ToString("o")
            };
        }).ToList();

        return Results.Ok(new BffSearchResponse<BffDiscussionSearchItem>
        {
            Items = items,
            Offset = result.Offset,
            PageSize = result.PageSize,
            HasMoreItems = result.HasMoreItems
        });
    }

    private static async Task<IResult> SearchPostsAsync(
        SnakkApiClient apiClient,
        ICommunityContext communityContext,
        [FromQuery] string? q = null,
        [FromQuery] string? authorPublicId = null,
        [FromQuery] int pageSize = 20,
        [FromQuery] int offset = 0)
    {
        var result = await apiClient.SearchPostsAsync(
            query: q,
            authorPublicId: authorPublicId,
            discussionPublicId: null,
            spacePublicId: null,
            offset: offset,
            pageSize: pageSize);

        if (result is null)
            return Results.Ok(new BffSearchResponse<BffPostSearchItem> { Items = [], Offset = 0, PageSize = pageSize, HasMoreItems = false });

        var items = result.Items.Select(p =>
        {
            var slugId = SnakkUrlHelper.DiscussionSlugId(p.DiscussionSlug, p.DiscussionPublicId);
            var url = SnakkUrlHelper.Discussion(p.CommunitySlug, communityContext, p.Hub.Slug, p.Space.Slug, slugId);
            return new BffPostSearchItem
            {
                Url = url,
                DiscussionTitle = p.DiscussionTitle,
                HubName = p.Hub.Name,
                SpaceName = p.Space.Name,
                ContentPreview = p.ContentHighlight,
                CreatedAt = p.CreatedAt?.ToDateTime().ToString("o") ?? ""
            };
        }).ToList();

        return Results.Ok(new BffSearchResponse<BffPostSearchItem>
        {
            Items = items,
            Offset = result.Offset,
            PageSize = result.PageSize,
            HasMoreItems = result.HasMoreItems
        });
    }

    // Post endpoints
    private static async Task<IResult> GetDiscussionPostsAsync(
        string discussionId,
        [FromQuery] int offset,
        [FromQuery] int pageSize,
        SnakkApiClient apiClient)
    {
        var result = await apiClient.GetDiscussionPostsAsync(discussionId, offset, pageSize);
        if (result is null) return Results.NotFound();

        return Results.Ok(new
        {
            items = result.Items.Select(p => new
            {
                postNumber = p.PostNumber,
                publicId = p.PublicId,
                content = p.Content,
                renderedContent = p.RenderedContent,
                createdAt = p.CreatedAt.ToDateTime().ToString("O"),
                editedAt = p.EditedAt?.ToDateTime().ToString("O"),
                isFirstPost = p.IsFirstPost,
                isDeleted = p.IsDeleted,
                hasCodeBlock = p.HasCodeBlock,
                isUsersFirstPostInDiscussion = p.IsUsersFirstPostInDiscussion,
                isUsersFirstPostInSpace = p.IsUsersFirstPostInSpace,
                isOp = p.IsOp,
                isNecro = p.IsNecro,
                isMilestone = p.IsMilestone,
                createdByUserId = p.CreatedByUserId,
                author = p.Author is null ? null : new
                {
                    publicId = p.Author.PublicId,
                    displayName = p.Author.DisplayName,
                    avatarUrl = p.Author.AvatarUrl,
                    role = p.Author.Role,
                    isDeleted = p.Author.IsDeleted,
                    joinedAt = p.Author.JoinedAt?.ToDateTime().ToString("O"),
                    discussionCount = p.Author.DiscussionCount,
                    replyCount = p.Author.ReplyCount
                },
                replyTo = p.ReplyTo is null ? null : new
                {
                    authorName = p.ReplyTo.AuthorName,
                    contentSnippet = p.ReplyTo.ContentSnippet
                },
                reactions = new
                {
                    counts = p.Reactions?.Counts?.Counts
                        .ToDictionary(kvp => kvp.Key, kvp => (int)kvp.Value)
                        ?? new Dictionary<string, int>(),
                    userReactions = p.Reactions?.UserReactions?.Reactions.ToList()
                        ?? new List<string>()
                }
            }),
            offset = result.Offset,
            pageSize = result.PageSize,
            hasMoreItems = result.HasMoreItems,
            hasCodeBlocks = result.HasCodeBlocks
        });
    }

    private static async Task<IResult> EditPostAsync(
        string postId,
        [FromQuery] string content,
        SnakkApiClient apiClient)
    {
        var result = await apiClient.EditPostResultAsync(postId, content);

        if (!result.IsSuccess)
            return MapGrpcError(result.Status, result.Error);

        return Results.Ok();
    }

    private static async Task<IResult> DeletePostAsync(
        string postId,
        SnakkApiClient apiClient)
    {
        var success = await apiClient.DeletePostAsync(postId);

        if (!success)
            return Results.Content("<div class='alert alert-error'>Failed to delete post.</div>", "text/html");

        // Return tombstone HTML for HTMX swap
        return Results.Content(
            $@"<div id='post-{postId}' class='card bg-base-200 shadow-md mb-4 opacity-50'>
                <div class='card-body'>
                    <p class='italic text-base-content/60'>[This post has been deleted]</p>
                </div>
            </div>",
            "text/html");
    }

    private static async Task<IResult> GetPostHistoryAsync(
        string postId,
        IHttpClientFactory httpClientFactory,
        HttpContext httpContext)
    {
        var accessToken = httpContext.Request.Cookies[AuthCookieHelper.AccessCookieName];
        if (string.IsNullOrEmpty(accessToken))
            return Results.Unauthorized();

        var client = httpClientFactory.CreateClient("InternalApi");
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/posts/{postId}/history");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request, httpContext.RequestAborted);
        var body = await response.Content.ReadAsStringAsync(httpContext.RequestAborted);

        return Results.Content(body, "text/html", statusCode: (int)response.StatusCode);
    }

    // Discussion preview endpoint
    private static async Task<IResult> GetDiscussionPreviewAsync(
        string discussionId,
        SnakkApiClient apiClient)
    {
        var apiResult = await apiClient.GetDiscussionPreviewAsync(discussionId);
        if (apiResult is null) return Results.NotFound();

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
        if (apiResult is null) return Results.NotFound();

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
        if (apiResult is null) return Results.NotFound();

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
        if (apiResult is null) return Results.NotFound();

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
        if (apiResult is null) return Results.NotFound();

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
        return result is not null ? Results.Ok(result) : Results.NotFound();
    }

    // Moderation report reasons
    private static async Task<IResult> GetModerationReportReasonsAsync(
        [FromQuery] string? reportType,
        SnakkApiClient apiClient)
    {
        var result = await apiClient.GetReportReasonsAsync();
        return result is not null ? Results.Ok(result) : Results.Ok(Array.Empty<object>());
    }

    // --- Avatar upload proxy ---

    private static async Task<IResult> UploadAvatarBffAsync(
        IFormFile avatar,
        IHttpClientFactory httpClientFactory,
        Snakk.Protos.Auth.AuthService.AuthServiceClient authClient,
        HttpContext httpContext)
    {
        if (avatar is null || avatar.Length == 0)
            return Results.BadRequest(new { error = "No file provided." });

        var accessToken = httpContext.Request.Cookies[AuthCookieHelper.AccessCookieName];
        if (string.IsNullOrEmpty(accessToken))
            return Results.Unauthorized();

        using var content = new MultipartFormDataContent();
        using var fileStream = avatar.OpenReadStream();
        using var streamContent = new StreamContent(fileStream);
        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(avatar.ContentType);
        content.Add(streamContent, "avatar", avatar.FileName);

        var client = httpClientFactory.CreateClient("InternalApi");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/avatars/upload");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = content;

        var response = await client.SendAsync(request, httpContext.RequestAborted);
        var body = await response.Content.ReadAsStringAsync(httpContext.RequestAborted);

        // Refresh JWT so the AvatarFileName claim updates in the cookie
        if (response.IsSuccessStatusCode)
            await RefreshAuthCookiesAsync(httpContext, authClient);

        return Results.Content(body, "application/json", statusCode: (int)response.StatusCode);
    }

    // --- Avatar delete proxy ---

    private static async Task<IResult> DeleteAvatarBffAsync(
        IHttpClientFactory httpClientFactory,
        Snakk.Protos.Auth.AuthService.AuthServiceClient authClient,
        HttpContext httpContext)
    {
        var accessToken = httpContext.Request.Cookies[AuthCookieHelper.AccessCookieName];
        if (string.IsNullOrEmpty(accessToken))
            return Results.Unauthorized();

        var client = httpClientFactory.CreateClient("InternalApi");
        using var request = new HttpRequestMessage(HttpMethod.Delete, "/avatars");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request, httpContext.RequestAborted);
        var body = await response.Content.ReadAsStringAsync(httpContext.RequestAborted);

        // Refresh JWT so the AvatarFileName claim is cleared from the cookie
        if (response.IsSuccessStatusCode)
            await RefreshAuthCookiesAsync(httpContext, authClient);

        return Results.Content(body, "application/json", statusCode: (int)response.StatusCode);
    }

    // Media upload — proxies multipart to internal API
    private static async Task<IResult> UploadMediaAsync(
        IFormFile file,
        IHttpClientFactory httpClientFactory,
        HttpContext httpContext)
    {
        if (file is null || file.Length == 0)
            return Results.BadRequest(new { error = "No file provided." });

        var accessToken = httpContext.Request.Cookies[AuthCookieHelper.AccessCookieName];
        if (string.IsNullOrEmpty(accessToken))
            return Results.Unauthorized();

        using var content = new MultipartFormDataContent();
        using var fileStream = file.OpenReadStream();
        using var streamContent = new StreamContent(fileStream);
        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
        content.Add(streamContent, "file", file.FileName);

        var client = httpClientFactory.CreateClient("InternalApi");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/media/upload");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = content;

        var response = await client.SendAsync(request, httpContext.RequestAborted);
        var body = await response.Content.ReadAsStringAsync(httpContext.RequestAborted);

        return Results.Content(body, "application/json", statusCode: (int)response.StatusCode);
    }

    // Helper: map GrpcStatus to HTTP result
    // --- Media draft delete ---

    private static async Task<IResult> DeleteDraftMediaBffAsync(string url, IHttpClientFactory httpClientFactory, HttpContext httpContext)
    {
        var accessToken = httpContext.Request.Cookies[AuthCookieHelper.AccessCookieName];
        if (string.IsNullOrEmpty(accessToken)) return Results.Unauthorized();

        var client = httpClientFactory.CreateClient("InternalApi");
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/media/draft?url={Uri.EscapeDataString(url)}");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request, httpContext.RequestAborted);
        return Results.StatusCode((int)response.StatusCode);
    }

    // --- Question ---

    private static async Task<IResult> GetQuestionStatusBffAsync(string discussionId, SnakkApiClient apiClient)
    {
        var status = await apiClient.GetQuestionStatusAsync(discussionId);
        if (status is null) return Results.NotFound();
        return Results.Ok(new
        {
            isSolved = status.IsSolved,
            acceptedPostPublicId = status.HasAcceptedPostPublicId ? status.AcceptedPostPublicId : null,
            solvedAt = status.SolvedAt?.ToDateTime().ToString("o")
        });
    }

    private static async Task<IResult> MarkQuestionSolvedBffAsync(string discussionId, string postPublicId, SnakkApiClient apiClient)
    {
        var result = await apiClient.MarkQuestionSolvedAsync(discussionId, postPublicId);
        if (result is null) return Results.StatusCode(503);
        return result.Success
            ? Results.Ok(new { success = true })
            : Results.BadRequest(new { error = result.HasError ? result.Error : "Failed" });
    }

    // --- Debate ---

    private static async Task<IResult> GetDebateInfoBffAsync(string discussionId, SnakkApiClient apiClient)
    {
        var info = await apiClient.GetDebateInfoAsync(discussionId);
        if (info is null) return Results.NotFound();
        return Results.Ok(new
        {
            positions = info.Positions.Select(p => new { id = p.Id, label = p.Label, index = p.Index, postCount = p.PostCount }),
            allowNeutral = info.AllowNeutral,
            postPositions = info.PostPositions.ToDictionary(kv => kv.Key, kv => kv.Value)
        });
    }

    // --- Debate: Set position ---

    private static async Task<IResult> SetPostDebatePositionBffAsync(string discussionId, string postPublicId, int positionId, SnakkApiClient apiClient)
    {
        var result = await apiClient.SetPostDebatePositionAsync(discussionId, postPublicId, positionId);
        if (result is null) return Results.StatusCode(503);
        return result.Success
            ? Results.Ok(new { success = true })
            : Results.BadRequest(new { error = result.HasError ? result.Error : "Failed" });
    }

    // --- Journal: Add entry ---

    private static async Task<IResult> AddJournalEntryBffAsync(string discussionId, string postPublicId, SnakkApiClient apiClient)
    {
        var result = await apiClient.AddJournalEntryAsync(discussionId, postPublicId);
        if (result is null) return Results.StatusCode(503);
        return result.Success
            ? Results.Ok(new { success = true })
            : Results.BadRequest(new { error = result.HasError ? result.Error : "Failed" });
    }

    // --- Link ---

    private static async Task<IResult> GetDiscussionLinkBffAsync(string discussionId, SnakkApiClient apiClient)
    {
        var link = await apiClient.GetDiscussionLinkAsync(discussionId);
        if (link is null) return Results.NotFound();
        return Results.Ok(new
        {
            url = link.Url,
            title = link.HasTitle ? link.Title : null,
            description = link.HasDescription ? link.Description : null,
            imageUrl = link.HasImageUrl ? link.ImageUrl : null,
            domain = link.HasDomain ? link.Domain : null
        });
    }

    // --- Journal ---

    private static async Task<IResult> GetJournalEntriesBffAsync(string discussionId, SnakkApiClient apiClient)
    {
        var info = await apiClient.GetJournalEntriesAsync(discussionId);
        if (info is null) return Results.NotFound();
        return Results.Ok(new { entryPostPublicIds = info.EntryPostPublicIds.ToList() });
    }

    // --- Poll ---

    private static async Task<IResult> GetPollAsync(string discussionId, SnakkApiClient apiClient)
    {
        var poll = await apiClient.GetPollAsync(discussionId);
        if (poll is null)
            return Results.NotFound();

        return Results.Ok(new
        {
            options = poll.Options.Select(o => new { id = o.Id, text = o.Text, voteCount = o.VoteCount, displayOrder = o.DisplayOrder }),
            allowMultiple = poll.AllowMultiple,
            allowChangeVote = poll.AllowChangeVote,
            closesAt = poll.ClosesAt?.ToDateTime().ToString("o"),
            isClosed = poll.IsClosed,
            totalVotes = poll.TotalVotes,
            userVotedOptionIds = poll.UserVotedOptionIds.ToList()
        });
    }

    private static async Task<IResult> VotePollAsync(string discussionId, int optionId, SnakkApiClient apiClient)
    {
        var result = await apiClient.VotePollAsync(discussionId, optionId);
        if (result is null)
            return Results.StatusCode(503);

        return result.Success
            ? Results.Ok(new { success = true })
            : Results.BadRequest(new { error = result.HasError ? result.Error : "Vote failed" });
    }

    private static async Task<IResult> RemovePollVoteAsync(string discussionId, int optionId, SnakkApiClient apiClient)
    {
        var result = await apiClient.RemovePollVoteAsync(discussionId, optionId);
        if (result is null)
            return Results.StatusCode(503);

        return result.Success
            ? Results.Ok(new { success = true })
            : Results.BadRequest(new { error = result.HasError ? result.Error : "Remove vote failed" });
    }

    // --- Device Management ---

    private static async Task<IResult> GetMyDevicesAsync(
        HttpContext httpContext,
        IHttpClientFactory httpClientFactory)
    {
        var accessToken = httpContext.Request.Cookies[AuthCookieHelper.AccessCookieName]
            ?? httpContext.Request.Cookies[AuthCookieHelper.SessionCookieName];
        if (string.IsNullOrEmpty(accessToken)) return Results.Unauthorized();

        var client = httpClientFactory.CreateClient("InternalApi");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/auth/2fa/trusted-devices");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request);
        if (!response.IsSuccessStatusCode) return Results.StatusCode((int)response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        return Results.Content(content, "application/json");
    }

    private static async Task<IResult> RevokeMyDeviceAsync(
        string deviceId,
        HttpContext httpContext,
        IHttpClientFactory httpClientFactory)
    {
        if (!AuthCookieHelper.HasStrictAuthCookie(httpContext))
            return Results.StatusCode(403);

        var accessToken = httpContext.Request.Cookies[AuthCookieHelper.AccessCookieName];
        if (string.IsNullOrEmpty(accessToken)) return Results.Unauthorized();

        var client = httpClientFactory.CreateClient("InternalApi");
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/auth/2fa/trusted-devices/{deviceId}");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request);
        if (!response.IsSuccessStatusCode) return Results.StatusCode((int)response.StatusCode);

        return Results.Ok();
    }

    private static IResult MapGrpcError(GrpcStatus status, string? error = null) => status switch
    {
        GrpcStatus.Unauthenticated => Results.Unauthorized(),
        GrpcStatus.NotFound => Results.NotFound(),
        GrpcStatus.PermissionDenied => Results.Forbid(),
        GrpcStatus.InvalidArgument => Results.BadRequest(error is not null ? new { error } : null),
        _ => Results.StatusCode(503)
    };

    private static async Task<IResult> GenerateFeedTokenAsync(SnakkApiClient apiClient)
    {
        var token = await apiClient.GenerateFeedTokenAsync();
        if (token is null) return Results.StatusCode(503);
        return Results.Ok(new { token });
    }

    private static async Task<IResult> RevokeFeedTokenAsync(SnakkApiClient apiClient)
    {
        var success = await apiClient.RevokeFeedTokenAsync();
        return success ? Results.Ok() : Results.StatusCode(503);
    }
}

public record ToggleReactionRequest(int Type);
public record PreviewMarkupRequest(string Content);
public record BffCreateReportRequest(string EntityType, string EntityId, string Reason, string? Description);
public record ReadStateUpdate(string DiscussionId, string PostId);
public record BatchUpdateReadStatesRequest(List<ReadStateUpdate> Updates);
public record UpdateProfileRequestDto(string DisplayName, string? Password = null, string? TurnstileToken = null);
public record UpdatePreferencesRequestDto(bool? AutoFollowOnReply, string? Timezone = null, string? Bio = null);
