namespace Snakk.Web.Endpoints;

using System.Security.Claims;
using System.Security.Cryptography;
using Grpc.Core;
using Microsoft.AspNetCore.Mvc;
using Snakk.Protos.TwoFactor;
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

        // Authenticated-only sub-group â€” eliminates per-handler IsAuthenticated preambles
        var authGroup = group.MapGroup("").RequireAuthorization();

        // Homepage aggregated data
        group.MapGet("/homepage-data", GetHomepageDataAsync)
            .WithName("GetHomepageData");

        // Notifications
        authGroup.MapGet("/notifications", GetNotificationsAsync)
            .WithName("BffGetNotifications");

        authGroup.MapGet("/notifications/unread-count", GetUnreadNotificationCountAsync)
            .WithName("BffGetUnreadCount");

        authGroup.MapPost("/notifications/{notificationId}/read", MarkNotificationAsReadAsync)
            .WithName("BffMarkNotificationRead");

        authGroup.MapPost("/notifications/read-all", MarkAllNotificationsAsReadAsync)
            .WithName("BffMarkAllNotificationsRead");

        // Space follow actions
        authGroup.MapGet("/spaces/{spaceId}/follow-status", GetSpaceFollowStatusAsync)
            .WithName("BffGetSpaceFollowStatus");

        authGroup.MapPost("/spaces/{spaceId}/follow", ToggleSpaceFollowAsync)
            .WithName("BffToggleSpaceFollow");

        authGroup.MapPut("/spaces/{spaceId}/follow-level", SetSpaceFollowLevelAsync)
            .WithName("BffSetSpaceFollowLevel");

        // Discussion follow actions
        authGroup.MapGet("/discussions/{discussionId}/follow-status", GetDiscussionFollowStatusAsync)
            .WithName("BffGetDiscussionFollowStatus");

        authGroup.MapPost("/discussions/{discussionId}/follow", ToggleDiscussionFollowAsync)
            .WithName("BffToggleDiscussionFollow");

        authGroup.MapPost("/discussions/{discussionId}/mark-read", MarkDiscussionAsReadAsync)
            .WithName("BffMarkDiscussionRead");

        // Poll voting
        group.MapGet("/discussions/{discussionId}/poll", GetPollAsync)
            .WithName("BffGetPoll");
        authGroup.MapPost("/discussions/{discussionId}/poll/vote", VotePollAsync)
            .WithName("BffVotePoll")
            .RequireRateLimiting("flood-engage");
        authGroup.MapDelete("/discussions/{discussionId}/poll/vote", RemovePollVoteAsync)
            .WithName("BffRemovePollVote");

        // Question
        group.MapGet("/discussions/{discussionId}/question", GetQuestionStatusBffAsync)
            .WithName("BffGetQuestionStatus");
        authGroup.MapPost("/discussions/{discussionId}/question/solve", MarkQuestionSolvedBffAsync)
            .WithName("BffMarkQuestionSolved");

        // Debate
        group.MapGet("/discussions/{discussionId}/debate", GetDebateInfoBffAsync)
            .WithName("BffGetDebateInfo");

        authGroup.MapPost("/discussions/{discussionId}/debate/position", SetPostDebatePositionBffAsync)
            .WithName("BffSetPostDebatePosition")
            .RequireRateLimiting("flood-post");

        // Link
        group.MapGet("/discussions/{discussionId}/link", GetDiscussionLinkBffAsync)
            .WithName("BffGetDiscussionLink");

        // Journal
        group.MapGet("/discussions/{discussionId}/journal", GetJournalEntriesBffAsync)
            .WithName("BffGetJournalEntries");
        authGroup.MapPost("/discussions/{discussionId}/journal/entry", AddJournalEntryBffAsync)
            .WithName("BffAddJournalEntry")
            .RequireRateLimiting("flood-post");

        // IAmA
        group.MapGet("/discussions/{discussionId}/iama", GetIamaInfoBffAsync)
            .WithName("BffGetIamaInfo");
        authGroup.MapPost("/discussions/{discussionId}/iama/end", EndIamaSessionBffAsync)
            .WithName("BffEndIamaSession")
            .RequireRateLimiting("flood-engage");

        // Follow lists (for caching)
        authGroup.MapGet("/follows/spaces", GetFollowedSpacesAsync)
            .WithName("BffGetFollowedSpaces");

        authGroup.MapGet("/follows/discussions", GetFollowedDiscussionsAsync)
            .WithName("BffGetFollowedDiscussions");

        authGroup.MapGet("/follows/users", GetFollowedUsersAsync)
            .WithName("BffGetFollowedUsers");

        // Batch read states
        authGroup.MapPost("/read-states/batch", BatchUpdateReadStatesAsync)
            .WithName("BffBatchUpdateReadStates");

        // "New since last visit" feed
        authGroup.MapGet("/me/unread-count", GetUnreadSinceLastVisitCountAsync)
            .WithName("BffGetUnreadSinceLastVisitCount");

        authGroup.MapPost("/me/mark-all-read", MarkAllAsReadSinceLastVisitAsync)
            .WithName("BffMarkAllAsReadSinceLastVisit");

        // Post reactions
        group.MapGet("/posts/{postId}/reactions", GetPostReactionsAsync)
            .WithName("BffGetPostReactions");

        authGroup.MapGet("/posts/{postId}/reactions/me", GetMyPostReactionsAsync)
            .WithName("BffGetMyPostReactions");

        authGroup.MapPost("/posts/{postId}/reactions", TogglePostReactionAsync)
            .WithName("BffTogglePostReaction")
            .RequireRateLimiting("flood-engage");

        // Markup preview
        authGroup.MapPost("/markup/preview", PreviewMarkupAsync)
            .WithName("BffPreviewMarkup")
            .RequireRateLimiting("flood-post");

        // Moderation
        authGroup.MapPost("/moderation/reports", CreateReportAsync)
            .WithName("BffCreateReport")
            .RequireRateLimiting("flood-post");

        // Endless scroll data
        group.MapGet("/discussions/recent", GetRecentDiscussionsAsync)
            .WithName("BffGetRecentDiscussions");

        group.MapGet("/discussions/trending", GetTrendingDiscussionsAsync)
            .WithName("BffGetTrendingDiscussions");

        group.MapGet("/discussions/new", GetNewDiscussionsAsync)
            .WithName("BffGetNewDiscussions");

        group.MapGet("/spaces/{spaceId}/discussions", GetSpaceDiscussionsAsync)
            .WithName("BffGetSpaceDiscussions");

        // Auth operations
        group.MapGet("/auth/status", GetAuthStatusAsync)
            .WithName("BffGetAuthStatus");

        authGroup.MapPost("/auth/logout", LogoutAsync)
            .WithName("BffLogout");

        group.MapPost("/auth/refresh", RefreshTokenAsync)
            .WithName("BffRefreshToken")
            .AllowAnonymous(); // Refresh can happen before auth expires

        group.MapPost("/auth/login", ReloginAsync)
            .WithName("BffRelogin")
            .AllowAnonymous()
            .RequireRateLimiting("auth");

        group.MapPost("/auth/sudo", IssueSudoTokenBffAsync)
            .WithName("BffIssueSudoToken")
            .RequireAuthorization()
            .RequireRateLimiting("flood-post");

        group.MapPost("/auth/sudo/passkey/begin", BeginSudoPasskeyBffAsync)
            .WithName("BffBeginSudoPasskey")
            .RequireAuthorization()
            .RequireRateLimiting("flood-post");

        group.MapPost("/auth/sudo/passkey/complete", CompleteSudoPasskeyBffAsync)
            .WithName("BffCompleteSudoPasskey")
            .RequireAuthorization()
            .RequireRateLimiting("flood-post");

        // Current user (me) operations
        group.MapGet("/me", GetCurrentUserMeAsync)
            .WithName("BffGetCurrentUser");

        authGroup.MapPut("/me/profile", UpdateProfileMeAsync)
            .WithName("BffUpdateProfileMe");

        authGroup.MapPut("/me/preferences", UpdatePreferencesMeAsync)
            .WithName("BffUpdatePreferences");

        group.MapPost("/me/password", ChangePasswordBffAsync)
            .WithName("BffChangePassword")
            .RequireAuthorization();

        group.MapPost("/me/verify-credential", VerifyCredentialBffAsync)
            .WithName("BffVerifyCredential")
            .RequireAuthorization();

        // Adult-content interstitial â€” anonymous & authed
        group.MapPost("/adult-consent", AdultConsentAsync)
            .WithName("BffAdultConsent")
            .AllowAnonymous();

        group.MapPost("/adult-decline", AdultDeclineAsync)
            .WithName("BffAdultDecline")
            .AllowAnonymous();

        group.MapGet("/me/devices", GetMyDevicesAsync)
            .WithName("BffGetMyDevices")
            .RequireAuthorization();

        authGroup.MapDelete("/me/devices/{deviceId}", RevokeMyDeviceAsync)
            .WithName("BffRevokeMyDevice");

        // 2FA management — security-critical, middleware-enforced auth via authGroup
        authGroup.MapGet("/auth/2fa/status", Get2FAStatusBffAsync)
            .WithName("BffGet2FAStatus");

        authGroup.MapPost("/auth/2fa/setup", Setup2FABffAsync)
            .WithName("BffSetup2FA");

        authGroup.MapPost("/auth/2fa/enable", Enable2FABffAsync)
            .WithName("BffEnable2FA");

        authGroup.MapPost("/auth/2fa/disable", Disable2FABffAsync)
            .WithName("BffDisable2FA");

        authGroup.MapGet("/auth/2fa/backup-codes", GetBackupCodesBffAsync)
            .WithName("BffGetBackupCodes");

        authGroup.MapPost("/auth/2fa/backup-codes/regenerate", RegenerateBackupCodesBffAsync)
            .WithName("BffRegenerateBackupCodes");

        // User operations
        group.MapGet("/users/{userId}/stats", GetUserStatsAsync)
            .WithName("BffGetUserStats");

        group.MapGet("/users/{userId}/activity-history", GetUserActivityHistoryAsync)
            .WithName("BffGetUserActivityHistory");

        group.MapGet("/activity/sparkline", GetActivitySparklineAsync)
            .WithName("BffGetActivitySparkline");

        authGroup.MapGet("/users/{userId}/follow-status", GetUserFollowStatusAsync)
            .WithName("BffGetUserFollowStatus");

        authGroup.MapPost("/users/{userId}/follow", ToggleUserFollowAsync)
            .WithName("BffToggleUserFollow");

        authGroup.MapGet("/me/display-name-history", GetDisplayNameHistoryAsync)
            .WithName("BffGetDisplayNameHistory");

        authGroup.MapPost("/me/feed-token", GenerateFeedTokenAsync)
            .WithName("BffGenerateFeedToken");
        authGroup.MapDelete("/me/feed-token", RevokeFeedTokenAsync)
            .WithName("BffRevokeFeedToken");

        authGroup.MapPost("/me/discord/link-token", GenerateDiscordLinkTokenAsync)
            .WithName("BffGenerateDiscordLinkToken");
        authGroup.MapDelete("/me/discord", UnlinkDiscordAsync)
            .WithName("BffUnlinkDiscord");
        authGroup.MapGet("/me/discord/status", GetDiscordStatusAsync)
            .WithName("BffGetDiscordStatus");

        authGroup.MapGet("/me/sessions", GetMySessionsBffAsync)
            .WithName("BffGetMySessions");
        authGroup.MapDelete("/me/sessions/{sessionId}", RevokeMySessionBffAsync)
            .WithName("BffRevokeMySession")
            .RequireRateLimiting("flood-post");
        authGroup.MapDelete("/me/sessions", RevokeAllOtherSessionsBffAsync)
            .WithName("BffRevokeAllOtherSessions")
            .RequireRateLimiting("flood-post");
        authGroup.MapGet("/me/login-history", GetMyLoginHistoryBffAsync)
            .WithName("BffGetMyLoginHistory");

        // Search operations
        group.MapGet("/search/discussions", SearchDiscussionsAsync)
            .WithName("BffSearchDiscussions");

        group.MapGet("/search/posts", SearchPostsAsync)
            .WithName("BffSearchPosts");

        group.MapGet("/search/spaces", SearchSpacesAsync)
            .WithName("BffSearchSpaces");

        group.MapGet("/spaces/{spaceId}/allowed-types", GetSpaceAllowedTypesAsync)
            .WithName("BffGetSpaceAllowedTypes");

        authGroup.MapGet("/spaces/{spaceId}/info", GetSpaceInfoAsync)
            .WithName("BffGetSpaceInfo");

        // Post operations
        group.MapGet("/discussions/{discussionId}/posts", GetDiscussionPostsAsync)
            .WithName("BffGetDiscussionPosts");

        authGroup.MapPost("/posts/{postId}/edit", EditPostAsync)
            .WithName("BffEditPost")
            .RequireRateLimiting("flood-post");

        authGroup.MapPatch("/discussions/{discussionId}/title", EditDiscussionTitleAsync)
            .WithName("BffEditDiscussionTitle")
            .RequireRateLimiting("flood-post");

        authGroup.MapDelete("/posts/{postId}", DeletePostAsync)
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

        // Entity path resolution (for entity-link hover popups)
        group.MapGet("/entity/resolve", ResolveEntityPathAsync)
            .WithName("BffResolveEntityPath");

        // Moderation report reasons
        group.MapGet("/moderation/reports/reasons", GetModerationReportReasonsAsync)
            .WithName("BffGetModerationReportReasons");

        // Media upload + delete
        // DisableAntiforgery: multipart/form-data uploads can't use the antiforgery token header.
        // CSRF is mitigated by SameSite=Strict on the auth cookie + the mutation guard middleware.
        group.MapPost("/media/upload", UploadMediaAsync)
            .WithName("BffUploadMedia")
            .RequireAuthorization()
            .RequireRateLimiting("flood-upload")
            .DisableAntiforgery();
        group.MapDelete("/media/draft", DeleteDraftMediaBffAsync)
            .WithName("BffDeleteDraftMedia");

        // Avatar upload + delete (proxy to internal API)
        // DisableAntiforgery: same rationale as /media/upload above.
        group.MapPost("/avatars/upload", UploadAvatarBffAsync)
            .WithName("BffUploadAvatar")
            .RequireAuthorization()
            .RequireRateLimiting("flood-upload")
            .DisableAntiforgery();
        group.MapDelete("/avatars", DeleteAvatarBffAsync)
            .WithName("BffDeleteAvatar");

        // Entity avatar upload + delete (proxy to internal API)
        // DisableAntiforgery: same rationale as /media/upload above.
        group.MapPost("/avatars/upload/{entityType}/{entityId}", UploadEntityAvatarBffAsync)
            .WithName("BffUploadEntityAvatar")
            .RequireAuthorization()
            .DisableAntiforgery();
        group.MapDelete("/avatars/{entityType}/{entityId}", DeleteEntityAvatarBffAsync)
            .WithName("BffDeleteEntityAvatar");

        // Save / Bookmark
        authGroup.MapPost("/discussions/{discussionId}/save", ToggleSaveDiscussionAsync)
            .WithName("BffToggleSaveDiscussion");
        authGroup.MapPost("/posts/{postId}/save", ToggleSavePostAsync)
            .WithName("BffToggleSavePost");
        group.MapGet("/saves/discussion-ids", GetSavedDiscussionIdsAsync)
            .WithName("BffGetSavedDiscussionIds");
        group.MapGet("/saves/post-ids", GetSavedPostIdsAsync)
            .WithName("BffGetSavedPostIds");
        group.MapGet("/saves/counts", GetSaveCountsAsync)
            .WithName("BffGetSaveCounts");

        group.MapPost("/history/validate", ValidateHistoryIdsAsync)
            .WithName("BffValidateHistoryIds");

        // Social Links
        authGroup.MapGet("/me/social", GetMySocialLinksBffAsync)
            .WithName("BffGetMySocialLinks");
        authGroup.MapPut("/me/social", UpdateMySocialLinksBffAsync)
            .WithName("BffUpdateMySocialLinks");
        group.MapGet("/users/{publicId}/social", GetUserSocialLinksBffAsync)
            .WithName("BffGetUserSocialLinks");

        // Direct Messages
        authGroup.MapGet("/messages/unread-count", GetDmUnreadCountAsync)
            .WithName("BffGetDmUnreadCount");

        authGroup.MapGet("/messages/conversations", GetDmConversationsAsync)
            .WithName("BffGetDmConversations");

        authGroup.MapGet("/messages/conversations/{conversationId}", GetDmConversationAsync)
            .WithName("BffGetDmConversation");

        authGroup.MapPost("/messages/conversations", GetOrCreateDmConversationAsync)
            .WithName("BffGetOrCreateDmConversation")
            .RequireRateLimiting("flood-engage");

        authGroup.MapGet("/messages/conversations/{conversationId}/messages", GetDmMessagesAsync)
            .WithName("BffGetDmMessages");

        authGroup.MapPost("/messages/conversations/{conversationId}/send", SendDmMessageAsync)
            .WithName("BffSendDmMessage")
            .RequireRateLimiting("flood-post");

        authGroup.MapPost("/messages/conversations/{conversationId}/read", MarkDmAsReadAsync)
            .WithName("BffMarkDmAsRead");

        authGroup.MapDelete("/messages/conversations/{conversationId}/messages", DeleteDmMessagesAsync)
            .WithName("BffDeleteDmMessages");

        authGroup.MapDelete("/messages/conversations/{conversationId}", DeleteDmConversationAsync)
            .WithName("BffDeleteDmConversation");

        authGroup.MapPost("/messages/conversations/{conversationId}/pin", PinDmConversationAsync)
            .WithName("BffPinDmConversation");

        // GDPR
        group.MapDelete("/me/account", DeleteMyAccountBffAsync)
            .WithName("BffDeleteMyAccount")
            .RequireAuthorization()
            .RequireRateLimiting("auth");

        group.MapGet("/me/data-export", ExportMyDataBffAsync)
            .WithName("BffExportMyData")
            .RequireAuthorization()
            .RequireRateLimiting("expensive");
    }

    private static bool IsAuthenticated(HttpContext httpContext)
        => httpContext.User.Identity?.IsAuthenticated == true;

    // -- Streaming proxy helpers ------------------------------------------------

    /// <summary>
    /// Sends <paramref name="request"/> with ResponseHeadersRead so the upstream body is
    /// never buffered in the BFF process, then streams it directly into the client response.
    /// The <paramref name="response"/> is disposed only after the copy completes.
    /// </summary>
    private static async Task StreamProxyResponseAsync(
        HttpClient client,
        HttpRequestMessage request,
        HttpContext httpContext,
        string contentType,
        CancellationToken ct)
    {
        var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        httpContext.Response.StatusCode = (int)response.StatusCode;
        httpContext.Response.ContentType = contentType;
        try
        {
            await response.Content.CopyToAsync(httpContext.Response.Body, ct);
        }
        finally
        {
            response.Dispose();
        }
    }

    /// <summary>
    /// Sets a Cache-Control header on the response before returning a result.
    /// </summary>
    private static void SetCacheControl(HttpContext httpContext, string value)
        => httpContext.Response.Headers.CacheControl = value;

    private static async Task<IResult> GetHomepageDataAsync(
        [FromQuery] string? communityId,
        [FromQuery] int offset,
        [FromQuery] int pageSize,
        SnakkApiClient apiClient,
        HttpContext httpContext,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var viewerAllowsAdult = await Services.AdultContentGate.ViewerAllowsAdultAsync(httpContext, apiClient);
        // Aggregate multiple API calls in parallel
        var recentTask = apiClient.GetRecentDiscussionsAsync(offset, Math.Min(pageSize, MaxPageSize), communityId, viewerAllowsAdult: viewerAllowsAdult, ct: ct);
        var topActiveTask = apiClient.GetTopActiveDiscussionsAsync(communityId, viewerAllowsAdult: viewerAllowsAdult, ct: ct);
        var topSpacesTask = apiClient.GetTopActiveSpacesAsync(communityId, ct);
        var topContributorsTask = apiClient.GetTopContributorsAsync(communityId, ct);

        await Task.WhenAll(recentTask, topActiveTask, topSpacesTask, topContributorsTask);

        // Varies by viewer (adult-content filtering) — safe for browser cache only
        SetCacheControl(httpContext, "private, max-age=30");

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
        SnakkApiClient apiClient,
        HttpContext httpContext,
        CancellationToken ct)
    {

        var apiResult = await apiClient.GetNotificationsAsync(offset, Math.Min(pageSize, MaxPageSize), ct);
        if (apiResult?.Items is null)
        {
            return Results.Ok(new Models.Bff.BffNotificationsResponse
            {
                Items = []
            });
        }

        // Map API DTOs â†’ BFF DTOs
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

    private static async Task<IResult> GetUnreadNotificationCountAsync(SnakkApiClient apiClient,
        HttpContext httpContext,
        CancellationToken ct)
    {

        var apiResult = await apiClient.GetUnreadNotificationCountAsync(ct);

        // Map API DTO â†’ BFF DTO
        var bffResponse = new Models.Bff.BffNotificationCountResponse
        {
            Count = apiResult?.Count ?? 0
        };

        return Results.Ok(bffResponse);
    }

    private static async Task<IResult> MarkNotificationAsReadAsync(
        string notificationId,
        SnakkApiClient apiClient,
        HttpContext httpContext,
        CancellationToken ct)
    {

        await apiClient.MarkNotificationAsReadAsync(notificationId, ct);
        return Results.Ok();
    }

    private static async Task<IResult> MarkAllNotificationsAsReadAsync(SnakkApiClient apiClient,
        HttpContext httpContext,
        CancellationToken ct)
    {

        await apiClient.MarkAllNotificationsAsReadAsync(ct);
        return Results.Ok();
    }

    private static async Task<IResult> GetSpaceFollowStatusAsync(
        string spaceId,
        SnakkApiClient apiClient,
        HttpContext httpContext,
        CancellationToken ct)
    {

        var apiResult = await apiClient.GetSpaceFollowStatusAsync(spaceId, ct);
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
        SnakkApiClient apiClient,
        IFollowedSpacesCacheService followedSpacesCache,
        HttpContext httpContext,
        CancellationToken ct)
    {

        var result = await apiClient.ToggleSpaceFollowResultAsync(spaceId, level, ct);

        if (!result.IsSuccess)
            return MapGrpcError(result.Status, result.Error);

        var userId = httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? httpContext.User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.NameId)?.Value;
        if (userId is not null) await followedSpacesCache.InvalidateAsync(userId, ct);

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
        SnakkApiClient apiClient,
        HttpContext httpContext,
        CancellationToken ct)
    {

        var apiResult = await apiClient.SetSpaceFollowLevelAsync(spaceId, level, ct);
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
        SnakkApiClient apiClient,
        HttpContext httpContext,
        CancellationToken ct)
    {

        var apiResult = await apiClient.GetDiscussionFollowStatusAsync(discussionId, ct);
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
        SnakkApiClient apiClient,
        HttpContext httpContext,
        CancellationToken ct)
    {

        var result = await apiClient.ToggleDiscussionFollowResultAsync(discussionId, ct);

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
        SnakkApiClient apiClient,
        CancellationToken ct)
    {

        // Read userId from auth claims, not query params (IDOR prevention)
        var userId = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "";
        await apiClient.MarkDiscussionAsReadAsync(discussionId, userId, postId, ct);
        return Results.Ok();
    }

    private static async Task<IResult> GetPostReactionsAsync(
        string postId,
        SnakkApiClient apiClient,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var apiResult = await apiClient.GetPostReactionsAsync(postId, ct);

        var bffResponse = new Models.Bff.BffReactionsResponse
        {
            Counts = apiResult ?? new Dictionary<string, int>()
        };

        return Results.Ok(bffResponse);
    }

    private static async Task<IResult> GetMyPostReactionsAsync(
        string postId,
        SnakkApiClient apiClient,
        HttpContext httpContext,
        CancellationToken ct)
    {

        var apiResult = await apiClient.GetMyPostReactionsAsync(postId, ct);

        var bffResponse = new Models.Bff.BffMyReactionsResponse
        {
            Reactions = apiResult ?? []
        };

        return Results.Ok(bffResponse);
    }

    private static async Task<IResult> TogglePostReactionAsync(
        string postId,
        [FromBody] ToggleReactionRequest request,
        SnakkApiClient apiClient,
        HttpContext httpContext,
        CancellationToken ct)
    {

        await apiClient.TogglePostReactionAsync(postId, request.Type, ct);
        return Results.Ok();
    }

    private static async Task<IResult> PreviewMarkupAsync(
        [FromBody] PreviewMarkupRequest request,
        SnakkApiClient apiClient,
        HttpContext httpContext,
        CancellationToken ct)
    {

        var html = await apiClient.PreviewMarkupAsync(request.Content, ct);

        var bffResponse = new Models.Bff.BffMarkupPreviewResponse
        {
            Html = html ?? string.Empty
        };

        return Results.Ok(bffResponse);
    }

    private static async Task<IResult> CreateReportAsync(
        [FromBody] BffCreateReportRequest request,
        SnakkApiClient apiClient,
        HttpContext httpContext,
        CancellationToken ct)
    {

        var apiRequest = new Models.CreateReportRequest(
            request.EntityType,
            request.EntityId,
            request.Reason,
            request.Description,
            null); // Details
        await apiClient.CreateReportAsync(apiRequest, ct);

        return Results.Ok();
    }

    private static async Task<IResult> GetRecentDiscussionsAsync(
        [FromQuery] int offset,
        [FromQuery] int pageSize,
        [FromQuery] string? communityId,
        [FromQuery] string? cursor,
        SnakkApiClient apiClient,
        HttpContext httpContext,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (offset >= MaxDiscussionListOffset)
            return Results.BadRequest(new { error = "offset exceeds maximum pagination depth" });
        var viewerAllowsAdult = await Services.AdultContentGate.ViewerAllowsAdultAsync(httpContext, apiClient);
        var result = await apiClient.GetRecentDiscussionsAsync(offset, Math.Min(pageSize, MaxPageSize), communityId, cursor: cursor, viewerAllowsAdult: viewerAllowsAdult, ct: ct);
        // Varies by viewer (adult-content filtering) — safe for browser cache only
        SetCacheControl(httpContext, "private, max-age=30");
        return Results.Ok(result);
    }

    private static async Task<IResult> GetTrendingDiscussionsAsync(
        [FromQuery] int offset,
        [FromQuery] int pageSize,
        [FromQuery] string? communityId,
        SnakkApiClient apiClient,
        HttpContext httpContext,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (offset >= MaxDiscussionListOffset)
            return Results.BadRequest(new { error = "offset exceeds maximum pagination depth" });
        var viewerAllowsAdult = await Services.AdultContentGate.ViewerAllowsAdultAsync(httpContext, apiClient);
        var result = await apiClient.GetTrendingDiscussionsAsync(offset, Math.Min(pageSize, MaxPageSize), communityId, viewerAllowsAdult: viewerAllowsAdult, ct: ct);
        // Varies by viewer (adult-content filtering) — safe for browser cache only
        SetCacheControl(httpContext, "private, max-age=30");
        return Results.Ok(result);
    }

    private static async Task<IResult> GetNewDiscussionsAsync(
        [FromQuery] int offset,
        [FromQuery] int pageSize,
        [FromQuery] string? communityId,
        [FromQuery] string? cursor,
        SnakkApiClient apiClient,
        HttpContext httpContext,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (offset >= MaxDiscussionListOffset)
            return Results.BadRequest(new { error = "offset exceeds maximum pagination depth" });
        var viewerAllowsAdult = await Services.AdultContentGate.ViewerAllowsAdultAsync(httpContext, apiClient);
        var result = await apiClient.GetNewDiscussionsAsync(offset, Math.Min(pageSize, MaxPageSize), communityId, cursor, viewerAllowsAdult: viewerAllowsAdult, ct: ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetSpaceDiscussionsAsync(
        string spaceId,
        [FromQuery] int offset,
        [FromQuery] int pageSize,
        [FromQuery] string? cursor,
        SnakkApiClient apiClient,
        HttpContext httpContext,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (offset >= MaxDiscussionListOffset)
            return Results.BadRequest(new { error = "offset exceeds maximum pagination depth" });
        var viewerAllowsAdult = await Services.AdultContentGate.ViewerAllowsAdultAsync(httpContext, apiClient);
        var result = await apiClient.GetSpaceDiscussionsAsync(spaceId, offset, Math.Min(pageSize, MaxPageSize), cursor: cursor, viewerAllowsAdult: viewerAllowsAdult, ct: ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetFollowedSpacesAsync(SnakkApiClient apiClient,
        HttpContext httpContext,
        CancellationToken ct)
    {

        var apiResult = await apiClient.GetFollowedSpacesAsync(ct);

        var bffResponse = new Models.Bff.BffFollowedEntitiesResponse
        {
            Items = apiResult
        };

        return Results.Ok(bffResponse);
    }

    private static async Task<IResult> GetFollowedDiscussionsAsync(SnakkApiClient apiClient,
        HttpContext httpContext,
        CancellationToken ct)
    {

        var apiResult = await apiClient.GetFollowedDiscussionsAsync(ct);

        var bffResponse = new Models.Bff.BffFollowedEntitiesResponse
        {
            Items = apiResult
        };

        return Results.Ok(bffResponse);
    }

    private static async Task<IResult> GetFollowedUsersAsync(SnakkApiClient apiClient,
        HttpContext httpContext,
        CancellationToken ct)
    {

        var apiResult = await apiClient.GetFollowedUsersAsync(ct);

        var bffResponse = new Models.Bff.BffFollowedEntitiesResponse
        {
            Items = apiResult
        };

        return Results.Ok(bffResponse);
    }

    private static async Task<IResult> BatchUpdateReadStatesAsync(
        [FromBody] BatchUpdateReadStatesRequest request,
        SnakkApiClient apiClient,
        HttpContext httpContext,
        CancellationToken ct)
    {

        await apiClient.BatchUpdateReadStatesAsync(request.Updates.Select(u => (u.DiscussionId, u.PostId)).ToList(), ct);
        return Results.Ok();
    }

    private static async Task<IResult> GetUnreadSinceLastVisitCountAsync(
        SnakkApiClient apiClient,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var count = await apiClient.GetUnreadDiscussionCountAsync(ct);
        SetCacheControl(httpContext, "private, max-age=120");
        return Results.Ok(new { count });
    }

    private static async Task<IResult> MarkAllAsReadSinceLastVisitAsync(
        SnakkApiClient apiClient,
        HttpContext httpContext,
        CancellationToken ct)
    {
        await apiClient.MarkVisitAsReadAsync(ct);
        return Results.Ok();
    }

    // Auth endpoints
    private static async Task<IResult> GetAuthStatusAsync(SnakkApiClient apiClient, HttpContext httpContext, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var apiResult = await apiClient.GetAuthStatusAsync(ct);
        if (apiResult is null) return Results.Unauthorized();

        // Map API DTO â†’ BFF DTO (decouples frontend from API structure)
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

    private static async Task<IResult> LogoutAsync(SnakkApiClient apiClient, HttpContext httpContext, CancellationToken ct)
    {

        await apiClient.LogoutAsync(ct);
        AuthCookieHelper.DeleteAuthCookies(httpContext);

        return Results.Ok();
    }

    private static async Task<IResult> ReloginAsync(
        [FromBody] ReloginRequest body,
        Snakk.Protos.Auth.AuthService.AuthServiceClient authClient,
        HttpContext httpContext,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(body.Email) || string.IsNullOrWhiteSpace(body.Password))
            return Results.BadRequest();
        try
        {
            var response = await authClient.LoginAsync(new Snakk.Protos.Auth.LoginRequest
            {
                Email    = body.Email,
                Password = body.Password
            }, cancellationToken: ct);
            if (string.IsNullOrEmpty(response.AccessToken))
                return Results.Unauthorized();

            AuthCookieHelper.SetAuthCookies(httpContext, response.AccessToken, response.RefreshToken);
            httpContext.Response.Cookies.Delete(".Snakk.ReloginHint", new CookieOptions { Path = "/" });
            return Results.Ok(new { displayName = response.User?.DisplayName });
        }
        catch (RpcException ex) when (
            ex.StatusCode is StatusCode.Unauthenticated
                          or StatusCode.NotFound
                          or StatusCode.PermissionDenied)
        {
            return Results.Unauthorized();
        }
    }

    private static async Task<IResult> RefreshTokenAsync(
        HttpContext httpContext,
        Snakk.Protos.Auth.AuthService.AuthServiceClient authClient,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            // Read refresh token from cookie (not from request body)
            var currentRefreshToken = httpContext.Request.Cookies[AuthCookieHelper.RefreshCookieName];
            if (string.IsNullOrEmpty(currentRefreshToken))
                return Results.Unauthorized();

            var response = await authClient.RefreshTokenAsync(
                new Snakk.Protos.Auth.RefreshTokenRequest { RefreshToken = currentRefreshToken }, cancellationToken: ct);

            if (string.IsNullOrEmpty(response.AccessToken) || string.IsNullOrEmpty(response.RefreshToken))
            {
                // Token was rejected by the server â€” clear all auth cookies so the browser
                // is not stuck with a permanently revoked refresh token.
                AuthCookieHelper.DeleteAuthCookies(httpContext);
                return Results.Unauthorized();
            }

            // Set updated cookies (no tokens in response body)
            AuthCookieHelper.SetAuthCookies(httpContext, response.AccessToken, response.RefreshToken);
            return Results.Ok(new { needsConsent = response.NeedsConsent });
        }
        catch (RpcException ex) when (ex.StatusCode is StatusCode.Unauthenticated
                                          or StatusCode.NotFound or StatusCode.PermissionDenied)
        {
            // Explicit rejection â€” refresh token is invalid, force re-login.
            AuthCookieHelper.DeleteAuthCookies(httpContext);
            return Results.Unauthorized();
        }
        catch
        {
            // Transient failure (API restart, timeout, network blip) â€” preserve cookies
            // so the user isn't logged out just because the API was momentarily unavailable.
            return Results.StatusCode(503);
        }
    }

    /// <summary>
    /// Refreshes the auth cookies by requesting a new JWT via the refresh token.
    /// Used after operations that change JWT claims (e.g. avatar upload/delete).
    /// </summary>
    private static async Task RefreshAuthCookiesAsync(
        HttpContext httpContext,
        Snakk.Protos.Auth.AuthService.AuthServiceClient authClient,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            var refreshToken = httpContext.Request.Cookies[AuthCookieHelper.RefreshCookieName];
            if (string.IsNullOrEmpty(refreshToken)) return;

            var response = await authClient.RefreshTokenAsync(
                new Snakk.Protos.Auth.RefreshTokenRequest { RefreshToken = refreshToken }, cancellationToken: ct);

            if (!string.IsNullOrEmpty(response.AccessToken) && !string.IsNullOrEmpty(response.RefreshToken))
                AuthCookieHelper.SetAuthCookies(httpContext, response.AccessToken, response.RefreshToken);
        }
        catch { /* best-effort â€” avatar was saved, cookie refresh is non-critical */ }
    }

    // Current user (me) endpoints
    private static async Task<IResult> GetCurrentUserMeAsync(SnakkApiClient apiClient, HttpContext httpContext, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var apiResult = await apiClient.GetCurrentUserAsync(ct);
        if (apiResult is null) return Results.Unauthorized();

        // Sync timezone cookie for server-side rendering
        var timezone = apiResult.HasTimezone ? apiResult.Timezone : null;
        AuthCookieHelper.SetTimezoneCookie(httpContext, timezone);

        return Results.Ok(new
        {
            publicId = apiResult.PublicId,
            displayName = apiResult.DisplayName,
            emailVerified = apiResult.EmailVerified,
            connectedProviders = apiResult.ConnectedProviders,
            autoFollowOnReply = apiResult.AutoFollowOnReply,
            timezone = timezone,
            displayNameChangedAt = apiResult.DisplayNameChangedAt != null
                ? apiResult.DisplayNameChangedAt.ToDateTime().ToString("o")
                : null,
            isDisplayNameLocked = apiResult.IsDisplayNameLocked,
            hasPassword = apiResult.HasPassword,
            avatarUrl = apiResult.HasAvatarUrl ? apiResult.AvatarUrl : null,
            bio = apiResult.HasBio ? apiResult.Bio : null,
            feedToken = apiResult.HasFeedToken ? apiResult.FeedToken : null,
            allowAdultContent = apiResult.AllowAdultContent,
            adultPreviewImageMode = apiResult.AdultPreviewImageMode,
            hidePresence = apiResult.HidePresence
        });
    }

    private static async Task<IResult> GetDisplayNameHistoryAsync(SnakkApiClient apiClient,
        HttpContext httpContext,
        CancellationToken ct)
    {

        var result = await apiClient.GetDisplayNameHistoryAsync(ct);
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
        [FromBody] UpdateProfileWithSudoDto request,
        SnakkApiClient apiClient,
        HttpContext httpContext,
        CancellationToken ct)
    {

        var sudoToken = httpContext.Request.Cookies[AuthCookieHelper.SudoCookieName];
        var result = await apiClient.UpdateProfileAsync(request.DisplayName, sudoToken: sudoToken, ct: ct);

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
        HttpContext httpContext,
        CancellationToken ct)
    {

        var success = await apiClient.UpdatePreferencesAsync(request.AutoFollowOnReply, request.Timezone, request.Bio, request.AllowAdultContent, request.ClearAllowAdultContent, request.AdultPreviewImageMode, request.HidePresence, ct);
        if (!success) return Results.BadRequest(new { error = "Failed to update preferences" });

        // Update timezone cookie
        if (request.Timezone is not null)
            AuthCookieHelper.SetTimezoneCookie(httpContext, string.IsNullOrEmpty(request.Timezone) ? null : request.Timezone);

        return Results.Ok();
    }

    private static async Task<IResult> ChangePasswordBffAsync(
        HttpContext httpContext,
        IHttpClientFactory httpClientFactory,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var accessToken = httpContext.Request.Cookies[AuthCookieHelper.AccessCookieName];
        if (string.IsNullOrEmpty(accessToken)) return Results.Unauthorized();

        var sudoToken = httpContext.Request.Cookies[AuthCookieHelper.SudoCookieName];
        if (string.IsNullOrWhiteSpace(sudoToken))
            return Results.Json(new { error = "Authentication required." }, statusCode: 403);

        // Read client body (newPassword, confirmPassword) and inject sudoToken from cookie
        using var reader = new System.IO.StreamReader(httpContext.Request.Body);
        var rawBody = await reader.ReadToEndAsync(ct);
        System.Text.Json.JsonElement clientJson;
        try { clientJson = System.Text.Json.JsonDocument.Parse(rawBody).RootElement; }
        catch { return Results.BadRequest(new { error = "Invalid request body." }); }

        var merged = new System.Collections.Generic.Dictionary<string, object?>
        {
            ["sudoToken"] = sudoToken
        };
        foreach (var prop in clientJson.EnumerateObject())
            merged[prop.Name] = prop.Value.Clone();

        var client = httpClientFactory.CreateClient("InternalApi");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/me/password");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(merged),
            System.Text.Encoding.UTF8, "application/json");

        await StreamProxyResponseAsync(client, request, httpContext, "application/json", ct);
        return Results.Empty;
    }

    private static async Task<IResult> AdultConsentAsync(
        [FromForm] string? returnUrl,
        SnakkApiClient apiClient,
        HttpContext httpContext,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        // Authenticated users persist the preference; anonymous users get a session cookie.
        if (IsAuthenticated(httpContext))
        {
            await apiClient.UpdatePreferencesAsync(null, null, null, allowAdultContent: true, ct: ct);
        }
        else
        {
            httpContext.Response.Cookies.Append(
                AdultContentGate.ConfirmedCookie,
                "1",
                new CookieOptions
                {
                    HttpOnly = true,
                    Secure = httpContext.Request.IsHttps,
                    SameSite = SameSiteMode.Lax,
                    Expires = DateTimeOffset.UtcNow.AddYears(1)
                });
            httpContext.Response.Cookies.Delete(AdultContentGate.DeclinedCookie);
        }

        return Results.Redirect(SafeReturnUrl(returnUrl));
    }

    private static async Task<IResult> AdultDeclineAsync(
        [FromForm] string? returnUrl,
        SnakkApiClient apiClient,
        HttpContext httpContext,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (IsAuthenticated(httpContext))
        {
            await apiClient.UpdatePreferencesAsync(null, null, null, allowAdultContent: false, ct: ct);
        }
        else
        {
            httpContext.Response.Cookies.Append(
                AdultContentGate.DeclinedCookie,
                "1",
                new CookieOptions
                {
                    HttpOnly = true,
                    Secure = httpContext.Request.IsHttps,
                    SameSite = SameSiteMode.Lax,
                    Expires = DateTimeOffset.UtcNow.AddYears(1)
                });
            httpContext.Response.Cookies.Delete(AdultContentGate.ConfirmedCookie);
        }

        return Results.Redirect(SafeReturnUrl(returnUrl));
    }

    private static string SafeReturnUrl(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
            return "/";

        // Percent-decode first so encoded payloads like "/%2f/evil.com" (which the browser
        // normalizes to a protocol-relative "//evil.com") can't slip past the slash checks.
        string decoded;
        try { decoded = Uri.UnescapeDataString(returnUrl); }
        catch { return "/"; }

        return decoded.StartsWith('/')
            && !decoded.StartsWith("//") && !decoded.StartsWith("/\\")
            && !decoded.StartsWith("/%2f", StringComparison.OrdinalIgnoreCase)
            ? returnUrl
            : "/";
    }

    // User endpoints
    private static async Task<IResult> GetUserStatsAsync(
        string userId,
        SnakkApiClient apiClient,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var apiResult = await apiClient.GetUserStatsAsync(userId, ct);
        if (apiResult is null) return Results.NotFound();

        var bffResponse = new Models.Bff.BffUserStatsResponse
        {
            PublicId = apiResult.PublicId,
            DisplayName = apiResult.DisplayName,
            AvatarUrl = apiResult.AvatarUrl,
            AvatarThumbnailUrl = apiResult.HasAvatarThumbnailUrl ? apiResult.AvatarThumbnailUrl : null,
            Bio = apiResult.HasBio ? apiResult.Bio : null,
            DiscussionCount = apiResult.DiscussionCount,
            ReplyCount = apiResult.ReplyCount,
            FollowerCount = apiResult.FollowerCount,
            FollowingCount = apiResult.FollowingCount,
            GradientCss = Snakk.Shared.Avatars.AvatarGenerator.GenerateGradientCss($"user:{apiResult.PublicId}")
        };

        return Results.Ok(bffResponse);
    }

    private static async Task<IResult> GetUserActivityHistoryAsync(
        string userId,
        [FromQuery] int days,
        SnakkApiClient apiClient,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var apiResult = await apiClient.GetUserActivityHistoryAsync(userId, days, ct);
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
        SnakkApiClient apiClient,
        CancellationToken ct)
    {

        // Read currentUserId from auth claims, not query params (IDOR prevention)
        var currentUserId = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "";
        var apiResult = await apiClient.GetUserFollowStatusAsync(userId, currentUserId, ct);
        if (apiResult is null) return Results.NotFound();

        var bffResponse = new Models.Bff.BffUserFollowStatusResponse
        {
            IsFollowing = apiResult.IsFollowing
        };

        return Results.Ok(bffResponse);
    }

    private static async Task<IResult> ToggleUserFollowAsync(
        string userId,
        SnakkApiClient apiClient,
        HttpContext httpContext,
        CancellationToken ct)
    {

        var result = await apiClient.ToggleUserFollowResultAsync(userId, ct);

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
        HttpContext httpContext,
        SnakkApiClient apiClient,
        ICommunityContext communityContext,
        CancellationToken ct,
        [FromQuery] string? q = null,
        [FromQuery] string? authorPublicId = null,
        [FromQuery] int pageSize = 20,
        [FromQuery] int offset = 0)
    {
        ct.ThrowIfCancellationRequested();
        var viewerAllowsAdult = await Services.AdultContentGate.ViewerAllowsAdultAsync(httpContext, apiClient);
        var result = await apiClient.SearchDiscussionsAsync(
            query: q,
            authorPublicId: authorPublicId,
            spacePublicId: null,
            hubPublicId: null,
            offset: offset,
            pageSize: Math.Min(pageSize, MaxPageSize),
            viewerAllowsAdult: viewerAllowsAdult,
            ct: ct);

        if (result is null)
            return Results.Ok(new BffSearchResponse<BffDiscussionSearchItem> { Items = [], Offset = 0, PageSize = pageSize, HasMoreItems = false });

        var items = result.Items.Select(d =>
        {
            var slugId = SnakkUrlHelper.DiscussionSlugId(d.Slug, d.PublicId);
            var url = SnakkUrlHelper.Discussion(d.Community.Slug, communityContext, d.Hub.Slug, d.Space.Slug, slugId);
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
        CancellationToken ct,
        [FromQuery] string? q = null,
        [FromQuery] string? authorPublicId = null,
        [FromQuery] int pageSize = 20,
        [FromQuery] int offset = 0)
    {
        ct.ThrowIfCancellationRequested();
        var empty = new BffSearchResponse<BffPostSearchItem> { Items = [], Offset = 0, PageSize = pageSize, HasMoreItems = false };
        try
        {
            var result = await apiClient.SearchPostsAsync(
                query: q,
                authorPublicId: authorPublicId,
                discussionPublicId: null,
                spacePublicId: null,
                offset: offset,
                pageSize: Math.Min(pageSize, MaxPageSize),
                ct: ct);

            if (result is null) return Results.Ok(empty);

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
                    SpaceGradientCss = GradientHelper.SpaceGradient(p.Space.PublicId),
                    ContentPreview = p.ContentHighlight,
                    CreatedAt = p.CreatedAt?.ToDateTime().ToString("o") ?? "",
                    AuthorPublicId = p.Author.PublicId,
                    AuthorDisplayName = p.Author.DisplayName,
                    AuthorAvatarUrl = p.Author.HasAvatarUrl
                        ? p.Author.AvatarUrl
                        : SnakkUrlHelper.UserAvatar(p.Author.PublicId),
                    PostPublicId = p.PublicId,
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
        catch { return Results.Ok(empty); }
    }

    private static async Task<IResult> GetSpaceAllowedTypesAsync(
        string spaceId,
        SnakkApiClient apiClient,
        HttpContext httpContext,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var accessToken = httpContext.Request.Cookies[AuthCookieHelper.AccessCookieName];
        if (string.IsNullOrEmpty(accessToken))
            return Results.Unauthorized();

        var space = await apiClient.GetSpaceAsync(spaceId, ct);

        if (space is null)
            return Results.NotFound();
        var allowedTypes = (space.AllowedDiscussionTypes.Count > 0
            ? space.AllowedDiscussionTypes.ToList()
            : new List<int> { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 })
            .Where(t => t != 3)
            .OrderBy(t => t)
            .ToList();

        return Results.Ok(new { allowedTypes });
    }

    private static async Task<IResult> GetSpaceInfoAsync(
        string spaceId,
        SnakkApiClient apiClient,
        HttpContext httpContext,
        CancellationToken ct)
    {

        var space = await apiClient.GetSpaceAsync(spaceId, ct);
        if (space is null) return Results.NotFound();

        // Fetch hub + community once â€” reused for both access check and response payload
        var hub = !string.IsNullOrEmpty(space.HubSlug)
            ? await apiClient.GetHubBySlugResultAsync(space.HubSlug, space.CommunitySlug, ct)
            : null;
        var community = !string.IsNullOrEmpty(space.CommunitySlug)
            ? await apiClient.GetCommunityBySlugAsync(space.CommunitySlug, ct)
            : null;

        // Access check â€” verify user can see this space (restricted spaces/hubs/communities)
        if (space.IsRestricted)
        {
            var access = await apiClient.CheckGroupAccessAsync(
                community?.PublicId!,
                hub?.IsSuccess == true ? hub.Value?.PublicId : null,
                space.PublicId,
                ct);

            if (access is not null && access.AccessLevel < 1)
                return Results.NotFound(); // Don't reveal existence
        }

        return Results.Ok(new
        {
            publicId = space.PublicId,
            name = space.Name,
            slug = space.Slug,
            hubSlug = space.HubSlug,
            hubName = hub?.IsSuccess == true ? hub.Value?.Name ?? "" : "",
            communitySlug = space.CommunitySlug,
            discussionCount = space.DiscussionCount,
            communityName = community?.Name ?? "",
            avatarUrl = Snakk.Shared.Helpers.AvatarHelper.GetAvatarUrl(space.PublicId, Snakk.Shared.Helpers.AvatarEntityType.Space, 0)
        });
    }

    private static async Task<IResult> SearchSpacesAsync(
        SnakkApiClient apiClient,
        CancellationToken ct,
        [FromQuery] string? q = null,
        [FromQuery] string? hubId = null,
        [FromQuery] string? communityId = null,
        [FromQuery] int limit = 10)
    {
        ct.ThrowIfCancellationRequested();
        var result = await apiClient.SearchSpacesAsync(q, hubId, communityId, Math.Min(limit, 20), ct);

        if (result is null)
            return Results.Ok(Array.Empty<object>());

        var items = result.Items.Select(s => new
        {
            publicId = s.PublicId,
            name = s.Name,
            slug = s.Slug,
            hubSlug = s.HubSlug,
            hubName = s.HubName,
            communitySlug = s.CommunitySlug,
            discussionCount = s.DiscussionCount,
            communityName = s.CommunityName,
            avatarUrl = s.AvatarUrl
        });

        return Results.Ok(items);
    }

    // Post endpoints
    private static async Task<IResult> GetDiscussionPostsAsync(
        string discussionId,
        [FromQuery] int offset,
        [FromQuery] int pageSize,
        [FromQuery] string? discussionType,
        SnakkApiClient apiClient,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var fetchPageSize = discussionType == "Iama"
            ? Math.Min(pageSize + 1, MaxPageSize)
            : Math.Min(pageSize, MaxPageSize);
        var result = await apiClient.GetDiscussionPostsAsync(discussionId, offset, fetchPageSize, ct);
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
                    avatarThumbnailUrl = p.Author.HasAvatarThumbnailUrl ? p.Author.AvatarThumbnailUrl : null,
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

    private record EditPostRequest(string Content);

    private static async Task<IResult> EditPostAsync(
        string postId,
        [FromBody] EditPostRequest body,
        SnakkApiClient apiClient,
        HttpContext httpContext,
        CancellationToken ct)
    {

        var result = await apiClient.EditPostResultAsync(postId, body.Content, ct);

        if (!result.IsSuccess)
            return MapGrpcError(result.Status, result.Error);

        return Results.Content(result.Value!.RenderedHtml, "text/html");
    }

    private record EditDiscussionTitleRequest(string Title);

    private static async Task<IResult> EditDiscussionTitleAsync(
        string discussionId,
        [FromBody] EditDiscussionTitleRequest body,
        SnakkApiClient apiClient,
        HttpContext httpContext,
        CancellationToken ct)
    {

        var result = await apiClient.EditDiscussionTitleResultAsync(discussionId, body.Title, ct);

        if (!result.IsSuccess)
            return MapGrpcError(result.Status, result.Error);

        return Results.Ok();
    }

    private static async Task<IResult> DeletePostAsync(
        string postId,
        SnakkApiClient apiClient,
        HttpContext httpContext,
        CancellationToken ct)
    {

        var success = await apiClient.DeletePostAsync(postId, ct: ct);

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
        HttpContext httpContext,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var accessToken = httpContext.Request.Cookies[AuthCookieHelper.AccessCookieName];
        if (string.IsNullOrEmpty(accessToken))
            return Results.Unauthorized();

        var client = httpClientFactory.CreateClient("InternalApi");
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/posts/{postId}/history");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        await StreamProxyResponseAsync(client, request, httpContext, "text/html", ct);
        return Results.Empty;
    }

    // Discussion preview endpoint
    private static async Task<IResult> GetDiscussionPreviewAsync(
        string discussionId,
        SnakkApiClient apiClient,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var apiResult = await apiClient.GetDiscussionPreviewAsync(discussionId, ct);
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
        SnakkApiClient apiClient,
        HttpContext httpContext,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var apiResult = await apiClient.GetHubStatsAsync(publicId, ct);
        if (apiResult is null) return Results.NotFound();

        // Map API DTO â†’ BFF DTO
        var bffResponse = new Models.Bff.BffHubStatsResponse
        {
            PublicId = apiResult.PublicId,
            Name = apiResult.Name,
            Description = apiResult.Description,
            AvatarUrl = apiResult.AvatarUrl,
            SpaceCount = apiResult.SpaceCount,
            DiscussionCount = apiResult.DiscussionCount,
            ReplyCount = apiResult.ReplyCount,
            GradientCss = Snakk.Shared.Avatars.AvatarGenerator.GenerateGradientCss($"hub:{publicId}")
        };

        // Public entity stats — identical for all viewers
        SetCacheControl(httpContext, "public, max-age=60, s-maxage=60");
        return Results.Ok(bffResponse);
    }

    private static async Task<IResult> GetSpaceStatsForPopupAsync(
        string publicId,
        SnakkApiClient apiClient,
        HttpContext httpContext,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var apiResult = await apiClient.GetSpaceStatsAsync(publicId, ct);
        if (apiResult is null) return Results.NotFound();

        // Map API DTO â†’ BFF DTO
        var bffResponse = new Models.Bff.BffSpaceStatsResponse
        {
            PublicId = apiResult.PublicId,
            Name = apiResult.Name,
            Description = apiResult.Description,
            AvatarUrl = apiResult.AvatarUrl,
            DiscussionCount = apiResult.DiscussionCount,
            ReplyCount = apiResult.ReplyCount,
            FollowerCount = apiResult.FollowerCount,
            GradientCss = Snakk.Shared.Avatars.AvatarGenerator.GenerateGradientCss($"space:{publicId}")
        };

        // Public entity stats — identical for all viewers
        SetCacheControl(httpContext, "public, max-age=60, s-maxage=60");
        return Results.Ok(bffResponse);
    }

    private static async Task<IResult> GetCommunityStatsForPopupAsync(
        string publicId,
        SnakkApiClient apiClient,
        HttpContext httpContext,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var apiResult = await apiClient.GetCommunityStatsAsync(publicId, ct);
        if (apiResult is null) return Results.NotFound();

        // Map API DTO â†’ BFF DTO
        var bffResponse = new Models.Bff.BffCommunityStatsResponse
        {
            PublicId = apiResult.PublicId,
            Name = apiResult.Name,
            Description = apiResult.Description,
            AvatarUrl = apiResult.AvatarUrl,
            HubCount = apiResult.HubCount,
            SpaceCount = apiResult.SpaceCount,
            DiscussionCount = apiResult.DiscussionCount,
            ReplyCount = apiResult.ReplyCount,
            GradientCss = Snakk.Shared.Avatars.AvatarGenerator.GenerateGradientCss($"community:{publicId}")
        };

        // Public entity stats — identical for all viewers
        SetCacheControl(httpContext, "public, max-age=60, s-maxage=60");
        return Results.Ok(bffResponse);
    }

    private static async Task<IResult> GetUserStatsForPopupAsync(
        string publicId,
        SnakkApiClient apiClient,
        HttpContext httpContext,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var apiResult = await apiClient.GetUserStatsAsync(publicId, ct);
        if (apiResult is null) return Results.NotFound();

        var bffResponse = new Models.Bff.BffUserStatsResponse
        {
            PublicId = apiResult.PublicId,
            DisplayName = apiResult.DisplayName,
            AvatarUrl = apiResult.AvatarUrl,
            AvatarThumbnailUrl = apiResult.HasAvatarThumbnailUrl ? apiResult.AvatarThumbnailUrl : null,
            Bio = apiResult.HasBio ? apiResult.Bio : null,
            DiscussionCount = apiResult.DiscussionCount,
            ReplyCount = apiResult.ReplyCount,
            FollowerCount = apiResult.FollowerCount,
            FollowingCount = apiResult.FollowingCount,
            GradientCss = Snakk.Shared.Avatars.AvatarGenerator.GenerateGradientCss($"user:{apiResult.PublicId}"),
            IsGlobalAdmin = apiResult.IsGlobalAdmin
        };

        // Public user profile stats — identical for all viewers
        SetCacheControl(httpContext, "public, max-age=60, s-maxage=60");
        return Results.Ok(bffResponse);
    }

    private static async Task<IResult> GetDiscussionStatsForPopupAsync(
        string publicId,
        SnakkApiClient apiClient,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var result = await apiClient.GetDiscussionStatsForPopupAsync(publicId, ct);
        return result is not null ? Results.Ok(result) : Results.NotFound();
    }

    /// <summary>
    /// Resolves an internal entity path (e.g. /c/gaming/h/fps) to its type, publicId, and name.
    /// Used by the frontend to attach hover popups to entity-link elements in post content.
    /// </summary>
    private static async Task<IResult> ResolveEntityPathAsync(
        [FromQuery] string path,
        SnakkApiClient apiClient,
        IConfiguration configuration,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(path))
            return Results.BadRequest();

        path = path.TrimEnd('/');
        var defaultCommunitySlug = configuration["Snakk:DefaultCommunitySlug"] ?? "main";

        // Discussion: /c/{community}/h/{hub}/{space}/{slug~id} or /h/{hub}/{space}/{slug~id}
        var match = System.Text.RegularExpressions.Regex.Match(
            path, @"^(?:/c/[^/]+)?/h/[^/]+/[^/]+/([^/]+~([^/]+))$");
        if (match.Success)
        {
            var base62Id = match.Groups[2].Value;
            var publicId = Snakk.Shared.Helpers.UlidBase62.Decode(base62Id);
            var discussion = await apiClient.GetDiscussionAsync(publicId, ct);
            if (discussion is not null)
            {
                return Results.Ok(new Models.Bff.BffEntityResolveResponse
                {
                    Type = "discussion",
                    PublicId = discussion.PublicId,
                    Name = discussion.Title,
                });
            }

            return Results.NotFound();
        }

        // Space: /c/{community}/h/{hub}/{space} or /h/{hub}/{space}
        match = System.Text.RegularExpressions.Regex.Match(
            path, @"^(?:/c/[^/]+)?/h/([^/]+)/([^/]+)$");
        if (match.Success && !match.Groups[2].Value.Contains('~'))
        {
            var hubSlug = match.Groups[1].Value;
            var spaceSlug = match.Groups[2].Value;
            var space = await apiClient.GetSpaceBySlugAsync(spaceSlug, hubSlug, ct);
            if (space is not null)
            {
                return Results.Ok(new Models.Bff.BffEntityResolveResponse
                {
                    Type = "space",
                    PublicId = space.PublicId,
                    Name = space.Name,
                });
            }

            return Results.NotFound();
        }

        // Hub: /c/{community}/h/{hub} or /h/{hub}
        match = System.Text.RegularExpressions.Regex.Match(
            path, @"^(?:/c/([^/]+))?/h/([^/]+)$");
        if (match.Success)
        {
            var communitySlug = match.Groups[1].Value;
            if (string.IsNullOrEmpty(communitySlug))
                communitySlug = defaultCommunitySlug;

            var hubSlug = match.Groups[2].Value;
            var hub = await apiClient.GetHubBySlugAsync(hubSlug, communitySlug, ct);
            if (hub is not null)
            {
                return Results.Ok(new Models.Bff.BffEntityResolveResponse
                {
                    Type = "hub",
                    PublicId = hub.PublicId,
                    Name = hub.Name,
                });
            }

            return Results.NotFound();
        }

        // Community: /c/{slug}
        match = System.Text.RegularExpressions.Regex.Match(path, @"^/c/([^/]+)$");
        if (match.Success)
        {
            var slug = match.Groups[1].Value;
            var community = await apiClient.GetCommunityBySlugAsync(slug, ct);
            if (community is not null)
            {
                return Results.Ok(new Models.Bff.BffEntityResolveResponse
                {
                    Type = "community",
                    PublicId = community.PublicId,
                    Name = community.Name,
                });
            }

            return Results.NotFound();
        }

        // User: /u/{base62Id}
        match = System.Text.RegularExpressions.Regex.Match(path, @"^/u/([^/]+)$");
        if (match.Success)
        {
            var base62Id = match.Groups[1].Value;
            var publicId = Snakk.Shared.Helpers.UlidBase62.Decode(base62Id);
            var profile = await apiClient.GetUserProfileAsync(publicId, ct);
            if (profile is not null)
            {
                return Results.Ok(new Models.Bff.BffEntityResolveResponse
                {
                    Type = "user",
                    PublicId = publicId,
                    Name = profile.DisplayName,
                });
            }

            return Results.NotFound();
        }

        return Results.NotFound();
    }

    // Moderation report reasons
    private static async Task<IResult> GetModerationReportReasonsAsync(
        [FromQuery] string? reportType,
        SnakkApiClient apiClient,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var result = await apiClient.GetReportReasonsAsync(ct: ct);
        return result is not null ? Results.Ok(result) : Results.Ok(Array.Empty<object>());
    }

    // --- Avatar upload proxy ---

    private const int MaxPageSize = 100;

    // Hard cap on discussion-list pagination depth, aligned with the
    // EndlessScroll UI cap (MaxPages Ã— default pageSize = 10 Ã— 20). Blocks
    // trivial scraping of deep history via the public BFF surface.
    private const int MaxDiscussionListOffset = 200;

    private static readonly HashSet<string> AllowedImageTypes = new(StringComparer.OrdinalIgnoreCase)
        { "image/jpeg", "image/png", "image/webp", "image/gif" };
    private const long MaxUploadBytes = 5 * 1024 * 1024; // 5 MB

    // Image container signatures. Validates the actual bytes rather than trusting the
    // client-declared Content-Type before proxying an upload to the internal API
    // (S13 defense-in-depth; the API also re-validates + re-encodes via ImageSharp).
    private static readonly byte[][] _imageMagicSignatures =
    [
        [0xFF, 0xD8, 0xFF],                               // JPEG
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], // PNG
        [0x47, 0x49, 0x46, 0x38],                         // GIF87a / GIF89a
    ];

    private static bool IsValidImageMagic(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 4) return false;
        foreach (var sig in _imageMagicSignatures)
            if (bytes.Length >= sig.Length && bytes[..sig.Length].SequenceEqual(sig))
                return true;
        // WebP: "RIFF" <4-byte size> "WEBP"
        return bytes.Length >= 12
            && bytes[..4].SequenceEqual("RIFF"u8)
            && bytes.Slice(8, 4).SequenceEqual("WEBP"u8);
    }

    /// <summary>
    /// Buffers the uploaded file (capped at <see cref="MaxUploadBytes"/> by the callers) so the
    /// multipart content sent upstream is replayable across Polly retries. An IFormFile's
    /// ReferenceReadStream cannot be re-read or rewound once the first send attempt consumes it.
    /// </summary>
    private static async Task<byte[]> ReadFileBytesAsync(IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        using var buffer = new MemoryStream((int)file.Length);
        await stream.CopyToAsync(buffer, ct);
        return buffer.ToArray();
    }

    private static async Task<IResult> UploadAvatarBffAsync(
        IFormFile avatar,
        IHttpClientFactory httpClientFactory,
        Snakk.Protos.Auth.AuthService.AuthServiceClient authClient,
        HttpContext httpContext,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (avatar is null || avatar.Length == 0)
            return Results.BadRequest(new { error = "No file provided." });

        if (!AllowedImageTypes.Contains(avatar.ContentType))
            return Results.BadRequest(new { error = "File type not allowed. Use JPEG, PNG, WebP, or GIF." });

        if (avatar.Length > MaxUploadBytes)
            return Results.BadRequest(new { error = "File exceeds maximum size of 5 MB." });

        var accessToken = httpContext.Request.Cookies[AuthCookieHelper.AccessCookieName];
        if (string.IsNullOrEmpty(accessToken))
            return Results.Unauthorized();

        var safeFileName = Path.GetFileName(avatar.FileName);

        var fileBytes = await ReadFileBytesAsync(avatar, ct);
        if (!IsValidImageMagic(fileBytes))
            return Results.BadRequest(new { error = "File content is not a valid image." });

        using var content = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(avatar.ContentType);
        content.Add(fileContent, "avatar", safeFileName);

        var client = httpClientFactory.CreateClient("InternalApi");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/avatars/upload");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = content;

        var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

        // Refresh JWT so the AvatarFileName claim updates in the cookie (check status from headers, before streaming body)
        if (response.IsSuccessStatusCode)
            await RefreshAuthCookiesAsync(httpContext, authClient, ct);

        httpContext.Response.StatusCode = (int)response.StatusCode;
        httpContext.Response.ContentType = "application/json";
        try { await response.Content.CopyToAsync(httpContext.Response.Body, ct); }
        finally { response.Dispose(); }
        return Results.Empty;
    }

    // --- Avatar delete proxy ---

    private static async Task<IResult> DeleteAvatarBffAsync(
        IHttpClientFactory httpClientFactory,
        Snakk.Protos.Auth.AuthService.AuthServiceClient authClient,
        HttpContext httpContext,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var accessToken = httpContext.Request.Cookies[AuthCookieHelper.AccessCookieName];
        if (string.IsNullOrEmpty(accessToken))
            return Results.Unauthorized();

        var client = httpClientFactory.CreateClient("InternalApi");
        using var request = new HttpRequestMessage(HttpMethod.Delete, "/avatars");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

        // Refresh JWT so the AvatarFileName claim is cleared from the cookie (check status from headers, before streaming body)
        if (response.IsSuccessStatusCode)
            await RefreshAuthCookiesAsync(httpContext, authClient, ct);

        httpContext.Response.StatusCode = (int)response.StatusCode;
        httpContext.Response.ContentType = "application/json";
        try { await response.Content.CopyToAsync(httpContext.Response.Body, ct); }
        finally { response.Dispose(); }
        return Results.Empty;
    }

    // --- Entity avatar upload proxy ---

    private static async Task<IResult> UploadEntityAvatarBffAsync(
        string entityType,
        string entityId,
        IFormFile avatar,
        IHttpClientFactory httpClientFactory,
        HttpContext httpContext,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (avatar is null || avatar.Length == 0)
            return Results.BadRequest(new { error = "No file provided." });

        if (!AllowedImageTypes.Contains(avatar.ContentType))
            return Results.BadRequest(new { error = "File type not allowed. Use JPEG, PNG, WebP, or GIF." });

        if (avatar.Length > MaxUploadBytes)
            return Results.BadRequest(new { error = "File exceeds maximum size of 5 MB." });

        var accessToken = httpContext.Request.Cookies[AuthCookieHelper.AccessCookieName];
        if (string.IsNullOrEmpty(accessToken))
            return Results.Unauthorized();

        var safeFileName = Path.GetFileName(avatar.FileName);

        var fileBytes = await ReadFileBytesAsync(avatar, ct);
        if (!IsValidImageMagic(fileBytes))
            return Results.BadRequest(new { error = "File content is not a valid image." });

        using var content = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(avatar.ContentType);
        content.Add(fileContent, "avatar", safeFileName);

        var client = httpClientFactory.CreateClient("InternalApi");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/avatars/upload/{entityType}/{entityId}");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = content;

        await StreamProxyResponseAsync(client, request, httpContext, "application/json", ct);
        return Results.Empty;
    }

    // --- Entity avatar delete proxy ---

    private static async Task<IResult> DeleteEntityAvatarBffAsync(
        string entityType,
        string entityId,
        IHttpClientFactory httpClientFactory,
        HttpContext httpContext,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var accessToken = httpContext.Request.Cookies[AuthCookieHelper.AccessCookieName];
        if (string.IsNullOrEmpty(accessToken))
            return Results.Unauthorized();

        var client = httpClientFactory.CreateClient("InternalApi");
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/avatars/{entityType}/{entityId}");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        await StreamProxyResponseAsync(client, request, httpContext, "application/json", ct);
        return Results.Empty;
    }

    // Media upload — proxies multipart to internal API
    private static async Task<IResult> UploadMediaAsync(
        IFormFile file,
        IHttpClientFactory httpClientFactory,
        HttpContext httpContext,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (file is null || file.Length == 0)
            return Results.BadRequest(new { error = "No file provided." });

        if (!AllowedImageTypes.Contains(file.ContentType))
            return Results.BadRequest(new { error = "File type not allowed. Use JPEG, PNG, WebP, or GIF." });

        if (file.Length > MaxUploadBytes)
            return Results.BadRequest(new { error = "File exceeds maximum size of 5 MB." });

        var accessToken = httpContext.Request.Cookies[AuthCookieHelper.AccessCookieName];
        if (string.IsNullOrEmpty(accessToken))
            return Results.Unauthorized();

        var safeFileName = Path.GetFileName(file.FileName);

        var fileBytes = await ReadFileBytesAsync(file, ct);
        if (!IsValidImageMagic(fileBytes))
            return Results.BadRequest(new { error = "File content is not a valid image." });

        using var content = new MultipartFormDataContent();
        using var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
        content.Add(fileContent, "file", safeFileName);

        var client = httpClientFactory.CreateClient("InternalApi");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/media/upload");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = content;

        await StreamProxyResponseAsync(client, request, httpContext, "application/json", ct);
        return Results.Empty;
    }

    // Helper: map GrpcStatus to HTTP result
    // --- Media draft delete ---

    private static async Task<IResult> DeleteDraftMediaBffAsync(string url, IHttpClientFactory httpClientFactory, HttpContext httpContext, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var accessToken = httpContext.Request.Cookies[AuthCookieHelper.AccessCookieName];
        if (string.IsNullOrEmpty(accessToken)) return Results.Unauthorized();

        var client = httpClientFactory.CreateClient("InternalApi");
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"/media/draft?url={Uri.EscapeDataString(url)}");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.SendAsync(request, ct);
        return Results.StatusCode((int)response.StatusCode);
    }

    // --- Question ---

    private static async Task<IResult> GetQuestionStatusBffAsync(string discussionId, SnakkApiClient apiClient, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var status = await apiClient.GetQuestionStatusAsync(discussionId, ct);
        if (status is null) return Results.NotFound();
        return Results.Ok(new
        {
            isSolved = status.IsSolved,
            acceptedPostPublicId = status.HasAcceptedPostPublicId ? status.AcceptedPostPublicId : null,
            solvedAt = status.SolvedAt?.ToDateTime().ToString("o")
        });
    }

    private static async Task<IResult> MarkQuestionSolvedBffAsync(string discussionId, string postPublicId, SnakkApiClient apiClient,
        HttpContext httpContext, CancellationToken ct)
    {

        var result = await apiClient.MarkQuestionSolvedAsync(discussionId, postPublicId, ct);
        if (result is null) return Results.StatusCode(503);
        return result.Success
            ? Results.Ok(new { success = true })
            : Results.BadRequest(new { error = result.HasError ? result.Error : "Failed" });
    }

    // --- Debate ---

    private static async Task<IResult> GetDebateInfoBffAsync(string discussionId, SnakkApiClient apiClient, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var info = await apiClient.GetDebateInfoAsync(discussionId, ct);
        if (info is null) return Results.NotFound();
        return Results.Ok(new
        {
            positions = info.Positions.Select(p => new { id = p.Id, label = p.Label, index = p.Index, postCount = p.PostCount }),
            allowNeutral = info.AllowNeutral,
            postPositions = info.PostPositions.ToDictionary(kv => kv.Key, kv => kv.Value)
        });
    }

    // --- Debate: Set position ---

    private static async Task<IResult> SetPostDebatePositionBffAsync(string discussionId, string postPublicId, int positionId, SnakkApiClient apiClient,
        HttpContext httpContext, CancellationToken ct)
    {

        var result = await apiClient.SetPostDebatePositionAsync(discussionId, postPublicId, positionId, ct);
        if (result is null) return Results.StatusCode(503);
        return result.Success
            ? Results.Ok(new { success = true })
            : Results.BadRequest(new { error = result.HasError ? result.Error : "Failed" });
    }

    // --- Journal: Add entry ---

    private static async Task<IResult> AddJournalEntryBffAsync(string discussionId, string postPublicId, SnakkApiClient apiClient,
        HttpContext httpContext, CancellationToken ct)
    {

        var result = await apiClient.AddJournalEntryAsync(discussionId, postPublicId, ct);
        if (result is null) return Results.StatusCode(503);
        return result.Success
            ? Results.Ok(new { success = true })
            : Results.BadRequest(new { error = result.HasError ? result.Error : "Failed" });
    }

    // --- IAmA ---

    private static async Task<IResult> GetIamaInfoBffAsync(string discussionId, SnakkApiClient apiClient, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var info = await apiClient.GetIamaInfoAsync(discussionId, ct);
        if (info is null) return Results.NotFound();
        return Results.Ok(new
        {
            phase = info.Phase,
            isScheduled = info.IsScheduled,
            scheduledStartUtc = info.ScheduledStartUtc != null ? info.ScheduledStartUtc.ToDateTime().ToString("o") : null,
            scheduledEndUtc = info.ScheduledEndUtc != null ? info.ScheduledEndUtc.ToDateTime().ToString("o") : null,
            actualStartedAtUtc = info.ActualStartedAtUtc != null ? info.ActualStartedAtUtc.ToDateTime().ToString("o") : null,
            actualEndedAtUtc = info.ActualEndedAtUtc != null ? info.ActualEndedAtUtc.ToDateTime().ToString("o") : null,
            verificationNote = info.HasVerificationNote ? info.VerificationNote : null,
            verificationNoteHtml = info.HasVerificationNoteHtml ? info.VerificationNoteHtml : null,
            officialAnswers = info.OfficialAnswers.ToDictionary(kv => kv.Key, kv => kv.Value)
        });
    }

    private static async Task<IResult> EndIamaSessionBffAsync(string discussionId, SnakkApiClient apiClient,
        HttpContext httpContext, CancellationToken ct)
    {

        var result = await apiClient.TransitionIamaPhaseAsync(discussionId, 2, ct);
        if (result is null) return Results.StatusCode(503);
        return result.Success
            ? Results.Ok(new { success = true })
            : Results.BadRequest(new { error = result.HasError ? result.Error : "Failed" });
    }

    // --- Link ---

    private static async Task<IResult> GetDiscussionLinkBffAsync(string discussionId, SnakkApiClient apiClient, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var link = await apiClient.GetDiscussionLinkAsync(discussionId, ct);
        if (link is null) return Results.NotFound();
        return Results.Ok(new
        {
            url = link.Url,
            title = link.HasTitle ? link.Title : null,
            description = link.HasDescription ? link.Description : null,
            imageUrl = link.HasImageUrl ? link.ImageUrl : null,
            domain = link.HasDomain ? link.Domain : null,
            imagePathUrl = link.HasImagePathUrl ? link.ImagePathUrl : null,
            blurDataUri = link.HasBlurDataUri ? link.BlurDataUri : null,
            oembedHtml = link.HasOembedHtml ? link.OembedHtml : null,
            isInternal = link.IsInternal
        });
    }

    // --- Journal ---

    private static async Task<IResult> GetJournalEntriesBffAsync(string discussionId, SnakkApiClient apiClient, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var info = await apiClient.GetJournalEntriesAsync(discussionId, ct);
        if (info is null) return Results.NotFound();
        return Results.Ok(new { entryPostPublicIds = info.EntryPostPublicIds.ToList() });
    }

    // --- Poll ---

    private static async Task<IResult> GetPollAsync(string discussionId, SnakkApiClient apiClient, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var poll = await apiClient.GetPollAsync(discussionId, ct);
        if (poll is null)
            return Results.NotFound();

        return Results.Ok(new
        {
            options = poll.Options.Select(o => new { id = o.Id, text = o.Text, voteCount = o.VoteCount, displayOrder = o.DisplayOrder }),
            allowMultiple = poll.AllowMultiple,
            allowChangeVote = poll.AllowChangeVote,
            closesAt = poll.ClosesAt?.ToDateTime().ToString("o"),
            isClosed = poll.IsClosed,
            isSecret = poll.IsSecret,
            totalVotes = poll.TotalVotes,
            userVotedOptionIds = poll.UserVotedOptionIds.ToList(),
            isSegmented = poll.IsSegmented,
            segmentLabel = poll.HasSegmentLabel ? poll.SegmentLabel : null,
            segmentOptionA = poll.HasSegmentOptionA ? poll.SegmentOptionA : null,
            segmentOptionB = poll.HasSegmentOptionB ? poll.SegmentOptionB : null,
            userSegmentIndex = poll.HasUserSegmentIndex ? (int?)poll.UserSegmentIndex : null,
            segmentVotes = poll.SegmentVotes.Select(sv => new { optionId = sv.OptionId, segmentACount = sv.SegmentACount, segmentBCount = sv.SegmentBCount }).ToList()
        });
    }

    private static async Task<IResult> VotePollAsync(string discussionId, int optionId, SnakkApiClient apiClient,
        HttpContext httpContext, CancellationToken ct, int? segmentIndex = null)
    {

        var result = await apiClient.VotePollAsync(discussionId, optionId, segmentIndex, ct);
        if (result is null)
            return Results.StatusCode(503);

        return result.Success
            ? Results.Ok(new { success = true })
            : Results.BadRequest(new { error = result.HasError ? result.Error : "Vote failed" });
    }

    private static async Task<IResult> RemovePollVoteAsync(string discussionId, int optionId, SnakkApiClient apiClient,
        HttpContext httpContext, CancellationToken ct)
    {

        var result = await apiClient.RemovePollVoteAsync(discussionId, optionId, ct);
        if (result is null)
            return Results.StatusCode(503);

        return result.Success
            ? Results.Ok(new { success = true })
            : Results.BadRequest(new { error = result.HasError ? result.Error : "Remove vote failed" });
    }

    // --- 2FA Management ---

    private static async Task<IResult> Get2FAStatusBffAsync(
        HttpContext httpContext,
        IHttpClientFactory httpClientFactory,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        // Strict cookie only — do NOT fall back to the Lax session cookie. This 2FA status
        // read must not be reachable via a cross-site top-level navigation (S4).
        var accessToken = httpContext.Request.Cookies[AuthCookieHelper.AccessCookieName];
        if (string.IsNullOrEmpty(accessToken)) return Results.Unauthorized();

        var client = httpClientFactory.CreateClient("InternalApi");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/auth/2fa/status");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        await StreamProxyResponseAsync(client, request, httpContext, "application/json", ct);
        return Results.Empty;
    }

    private static async Task<IResult> Setup2FABffAsync(
        HttpContext httpContext,
        IHttpClientFactory httpClientFactory,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var accessToken = httpContext.Request.Cookies[AuthCookieHelper.AccessCookieName];
        if (string.IsNullOrEmpty(accessToken)) return Results.Unauthorized();

        var sudoToken = httpContext.Request.Cookies[AuthCookieHelper.SudoCookieName];
        if (string.IsNullOrWhiteSpace(sudoToken))
            return Results.Json(new { error = "Authentication required." }, statusCode: 403);

        var client = httpClientFactory.CreateClient("InternalApi");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/auth/2fa/setup");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(new { sudoToken }),
            System.Text.Encoding.UTF8, "application/json");

        await StreamProxyResponseAsync(client, request, httpContext, "application/json", ct);
        return Results.Empty;
    }

    private static async Task<IResult> VerifyCredentialBffAsync(
        HttpContext httpContext,
        IHttpClientFactory httpClientFactory,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var accessToken = httpContext.Request.Cookies[AuthCookieHelper.AccessCookieName];
        if (string.IsNullOrEmpty(accessToken)) return Results.Unauthorized();

        var client = httpClientFactory.CreateClient("InternalApi");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/me/verify-credential");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = new StreamContent(httpContext.Request.Body);
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

        await StreamProxyResponseAsync(client, request, httpContext, "application/json", ct);
        return Results.Empty;
    }

    private static async Task<bool> IsSudoValidBffAsync(
        HttpContext httpContext,
        IHttpClientFactory httpClientFactory,
        CancellationToken ct)
    {
        var sudoToken = httpContext.Request.Cookies[AuthCookieHelper.SudoCookieName];
        if (string.IsNullOrWhiteSpace(sudoToken)) return false;
        var accessToken = httpContext.Request.Cookies[AuthCookieHelper.AccessCookieName];
        if (string.IsNullOrEmpty(accessToken)) return false;

        var client = httpClientFactory.CreateClient("InternalApi");
        using var req = new HttpRequestMessage(HttpMethod.Post, "/me/verify-credential");
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        req.Content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(new { sudoToken }),
            System.Text.Encoding.UTF8, "application/json");

        var response2 = await client.SendAsync(req, ct);
        return response2.IsSuccessStatusCode;
    }

    private static async Task<IResult> IssueSudoTokenBffAsync(
        HttpContext httpContext,
        IHttpClientFactory httpClientFactory,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var accessToken = httpContext.Request.Cookies[AuthCookieHelper.AccessCookieName];
        if (string.IsNullOrEmpty(accessToken)) return Results.Unauthorized();

        var client = httpClientFactory.CreateClient("InternalApi");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/me/sudo");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = new StreamContent(httpContext.Request.Body);
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

        var response = await client.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            return Results.Content(body, "application/json", statusCode: (int)response.StatusCode);

        using var doc = System.Text.Json.JsonDocument.Parse(body);
        var root = doc.RootElement;
        var token = root.TryGetProperty("sudoToken", out var tp) ? tp.GetString() : null;
        var expires = root.TryGetProperty("expiresInSeconds", out var ep) ? ep.GetInt32() : 300;

        if (!string.IsNullOrEmpty(token))
            AuthCookieHelper.SetSudoCookie(httpContext, token, expires);

        return Results.Ok(new { expiresInSeconds = expires });
    }

    private static async Task<IResult> BeginSudoPasskeyBffAsync(
        HttpContext httpContext,
        IHttpClientFactory httpClientFactory,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var accessToken = httpContext.Request.Cookies[AuthCookieHelper.AccessCookieName];
        if (string.IsNullOrEmpty(accessToken)) return Results.Unauthorized();

        var client = httpClientFactory.CreateClient("InternalApi");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/me/sudo/passkey/begin");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = new StreamContent(httpContext.Request.Body);
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

        await StreamProxyResponseAsync(client, request, httpContext, "application/json", ct);
        return Results.Empty;
    }

    private static async Task<IResult> CompleteSudoPasskeyBffAsync(
        HttpContext httpContext,
        IHttpClientFactory httpClientFactory,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var accessToken = httpContext.Request.Cookies[AuthCookieHelper.AccessCookieName];
        if (string.IsNullOrEmpty(accessToken)) return Results.Unauthorized();

        var client = httpClientFactory.CreateClient("InternalApi");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/me/sudo/passkey/complete");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = new StreamContent(httpContext.Request.Body);
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

        var response = await client.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            return Results.Content(body, "application/json", statusCode: (int)response.StatusCode);

        using var doc = System.Text.Json.JsonDocument.Parse(body);
        var root = doc.RootElement;
        var token = root.TryGetProperty("sudoToken", out var tp) ? tp.GetString() : null;
        var expires = root.TryGetProperty("expiresInSeconds", out var ep) ? ep.GetInt32() : 300;

        if (!string.IsNullOrEmpty(token))
            AuthCookieHelper.SetSudoCookie(httpContext, token, expires);

        return Results.Ok(new { expiresInSeconds = expires });
    }

    private static async Task<IResult> Enable2FABffAsync(
        HttpContext httpContext,
        IHttpClientFactory httpClientFactory,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var accessToken = httpContext.Request.Cookies[AuthCookieHelper.AccessCookieName];
        if (string.IsNullOrEmpty(accessToken)) return Results.Unauthorized();

        var client = httpClientFactory.CreateClient("InternalApi");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/auth/2fa/enable");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = new StreamContent(httpContext.Request.Body);
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

        await StreamProxyResponseAsync(client, request, httpContext, "application/json", ct);
        return Results.Empty;
    }

    private static async Task<IResult> Disable2FABffAsync(
        HttpContext httpContext,
        IHttpClientFactory httpClientFactory,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var accessToken = httpContext.Request.Cookies[AuthCookieHelper.AccessCookieName];
        if (string.IsNullOrEmpty(accessToken)) return Results.Unauthorized();

        var sudoToken = httpContext.Request.Cookies[AuthCookieHelper.SudoCookieName];
        if (string.IsNullOrWhiteSpace(sudoToken))
            return Results.Json(new { error = "Authentication required." }, statusCode: 403);

        var client = httpClientFactory.CreateClient("InternalApi");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/auth/2fa/disable");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(new { sudoToken }),
            System.Text.Encoding.UTF8, "application/json");

        await StreamProxyResponseAsync(client, request, httpContext, "application/json", ct);
        return Results.Empty;
    }

    private static async Task<IResult> GetBackupCodesBffAsync(
        HttpContext httpContext,
        IHttpClientFactory httpClientFactory,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        // Strict cookie only — backup codes are highly sensitive and must never be reachable
        // via a cross-site top-level navigation carrying the Lax session cookie (S4).
        var accessToken = httpContext.Request.Cookies[AuthCookieHelper.AccessCookieName];
        if (string.IsNullOrEmpty(accessToken)) return Results.Unauthorized();

        var client = httpClientFactory.CreateClient("InternalApi");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/auth/2fa/backup-codes");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        await StreamProxyResponseAsync(client, request, httpContext, "application/json", ct);
        return Results.Empty;
    }

    private static async Task<IResult> RegenerateBackupCodesBffAsync(
        HttpContext httpContext,
        IHttpClientFactory httpClientFactory,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var accessToken = httpContext.Request.Cookies[AuthCookieHelper.AccessCookieName];
        if (string.IsNullOrEmpty(accessToken)) return Results.Unauthorized();

        var sudoToken = httpContext.Request.Cookies[AuthCookieHelper.SudoCookieName];
        if (string.IsNullOrWhiteSpace(sudoToken))
            return Results.Json(new { error = "Authentication required." }, statusCode: 403);

        var client = httpClientFactory.CreateClient("InternalApi");
        using var request = new HttpRequestMessage(HttpMethod.Post, "/auth/2fa/backup-codes/regenerate");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(new { sudoToken }),
            System.Text.Encoding.UTF8, "application/json");

        await StreamProxyResponseAsync(client, request, httpContext, "application/json", ct);
        return Results.Empty;
    }

    // --- Device Management ---

    private static async Task<IResult> GetMyDevicesAsync(
        TwoFactorService.TwoFactorServiceClient twoFactorClient,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            var response = await twoFactorClient.GetTrustedDevicesAsync(
                new GetTrustedDevicesRequest(), cancellationToken: ct);

            var devices = response.Devices.Select(d => new
            {
                publicId = d.PublicId,
                deviceName = d.DeviceName,
                trustedAt = d.TrustedAt,
                expiresAt = string.IsNullOrEmpty(d.ExpiresAt) ? null : d.ExpiresAt,
                lastUsedAt = string.IsNullOrEmpty(d.LastUsedAt) ? null : d.LastUsedAt,
                lastUsedIp = string.IsNullOrEmpty(d.LastUsedIp) ? null : d.LastUsedIp,
                isActive = d.IsActive
            });

            return Results.Ok(devices);
        }
        catch (RpcException ex)
        {
            return GrpcResult<object>.FromRpcException(ex).Status switch
            {
                GrpcStatus.Unauthenticated => Results.Unauthorized(),
                _ => Results.StatusCode(503)
            };
        }
    }

    private static async Task<IResult> RevokeMyDeviceAsync(
        string deviceId,
        HttpContext httpContext,
        TwoFactorService.TwoFactorServiceClient twoFactorClient,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!AuthCookieHelper.HasStrictAuthCookie(httpContext))
            return Results.StatusCode(403);

        try
        {
            await twoFactorClient.RevokeDeviceAsync(
                new RevokeDeviceRequest { DeviceId = deviceId }, cancellationToken: ct);
            return Results.Ok();
        }
        catch (RpcException ex)
        {
            return GrpcResult<object>.FromRpcException(ex).Status switch
            {
                GrpcStatus.Unauthenticated => Results.Unauthorized(),
                GrpcStatus.NotFound => Results.NotFound(),
                _ => Results.StatusCode(503)
            };
        }
    }

    private static IResult MapGrpcError(GrpcStatus status, string? error = null) => status switch
    {
        GrpcStatus.Unauthenticated => Results.Unauthorized(),
        GrpcStatus.NotFound => Results.NotFound(),
        GrpcStatus.PermissionDenied => Results.Forbid(),
        GrpcStatus.InvalidArgument => Results.BadRequest(error is not null ? new { error } : null),
        _ => Results.StatusCode(503)
    };

    private static async Task<IResult> GenerateFeedTokenAsync(
        SnakkApiClient apiClient,
        HttpContext httpContext,
        IHttpClientFactory httpClientFactory,
        CancellationToken ct)
    {

        if (!await IsSudoValidBffAsync(httpContext, httpClientFactory, ct))
            return Results.Json(new { error = "Authentication required." }, statusCode: 403);

        var token = await apiClient.GenerateFeedTokenAsync(ct);
        if (token is null) return Results.StatusCode(503);
        return Results.Ok(new { token });
    }

    private static async Task<IResult> RevokeFeedTokenAsync(
        SnakkApiClient apiClient,
        HttpContext httpContext,
        IHttpClientFactory httpClientFactory,
        CancellationToken ct)
    {

        if (!await IsSudoValidBffAsync(httpContext, httpClientFactory, ct))
            return Results.Json(new { error = "Authentication required." }, statusCode: 403);

        var success = await apiClient.RevokeFeedTokenAsync(ct);
        return success ? Results.Ok() : Results.StatusCode(503);
    }

    private static async Task<IResult> GenerateDiscordLinkTokenAsync(
        SnakkApiClient apiClient,
        HttpContext httpContext,
        IHttpClientFactory httpClientFactory,
        CancellationToken ct)
    {

        if (!await IsSudoValidBffAsync(httpContext, httpClientFactory, ct))
            return Results.Json(new { error = "Authentication required." }, statusCode: 403);

        var result = await apiClient.GenerateDiscordLinkTokenAsync(ct);
        if (result is null) return Results.StatusCode(503);
        var linkUrl = $"/auth/discord-link/challenge?linkToken={Uri.EscapeDataString(result.Token)}";
        return Results.Ok(new { token = result.Token, linkUrl });
    }

    private static async Task<IResult> UnlinkDiscordAsync(
        SnakkApiClient apiClient,
        HttpContext httpContext,
        IHttpClientFactory httpClientFactory,
        CancellationToken ct)
    {

        if (!await IsSudoValidBffAsync(httpContext, httpClientFactory, ct))
            return Results.Json(new { error = "Authentication required." }, statusCode: 403);

        var success = await apiClient.UnlinkDiscordAsync(ct);
        return success ? Results.Ok() : Results.StatusCode(503);
    }

    private static async Task<IResult> GetDiscordStatusAsync(SnakkApiClient apiClient,
        HttpContext httpContext,
        CancellationToken ct)
    {

        var status = await apiClient.GetDiscordStatusAsync(ct);
        if (status is null) return Results.StatusCode(503);
        return Results.Ok(new
        {
            isLinked = status.IsLinked,
            discordUsername = status.HasDiscordUsername ? status.DiscordUsername : null,
            discordUserId = status.HasDiscordUserId ? status.DiscordUserId : null
        });
    }

    private static async Task<IResult> GetMySessionsBffAsync(
        HttpContext httpContext,
        IHttpClientFactory httpClientFactory,
        CancellationToken ct)
    {
        var accessToken = httpContext.Request.Cookies[AuthCookieHelper.AccessCookieName];
        if (string.IsNullOrEmpty(accessToken)) return Results.Unauthorized();

        var client = httpClientFactory.CreateClient("InternalApi");
        using var req = new HttpRequestMessage(HttpMethod.Get, "/sessions/");
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var rawRefresh = httpContext.Request.Cookies[AuthCookieHelper.RefreshCookieName];
        if (!string.IsNullOrEmpty(rawRefresh))
        {
            var hash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawRefresh)));
            req.Headers.TryAddWithoutValidation("X-Current-Refresh-Token-Hash", hash);
        }

        await StreamProxyResponseAsync(client, req, httpContext, "application/json", ct);
        return Results.Empty;
    }

    private static async Task<IResult> RevokeMySessionBffAsync(
        string sessionId,
        HttpContext httpContext,
        IHttpClientFactory httpClientFactory,
        CancellationToken ct)
    {

        if (!await IsSudoValidBffAsync(httpContext, httpClientFactory, ct))
            return Results.Json(new { error = "Authentication required." }, statusCode: 403);

        var sudoToken = httpContext.Request.Cookies[AuthCookieHelper.SudoCookieName];
        var accessToken = httpContext.Request.Cookies[AuthCookieHelper.AccessCookieName];
        var client = httpClientFactory.CreateClient("InternalApi");
        using var req = new HttpRequestMessage(HttpMethod.Delete, $"/sessions/{Uri.EscapeDataString(sessionId)}");
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken!);
        req.Content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(new { sudoToken }),
            System.Text.Encoding.UTF8, "application/json");

        await StreamProxyResponseAsync(client, req, httpContext, "application/json", ct);
        return Results.Empty;
    }

    private static async Task<IResult> RevokeAllOtherSessionsBffAsync(
        [FromBody] RevokeAllOtherBffRequest request,
        HttpContext httpContext,
        IHttpClientFactory httpClientFactory,
        CancellationToken ct)
    {

        if (!await IsSudoValidBffAsync(httpContext, httpClientFactory, ct))
            return Results.Json(new { error = "Authentication required." }, statusCode: 403);

        var sudoToken = httpContext.Request.Cookies[AuthCookieHelper.SudoCookieName];
        var accessToken = httpContext.Request.Cookies[AuthCookieHelper.AccessCookieName];
        var client = httpClientFactory.CreateClient("InternalApi");
        using var req = new HttpRequestMessage(HttpMethod.Delete, "/sessions/");
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken!);
        req.Content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(new { sudoToken, excludeSessionId = request.ExcludeSessionId }),
            System.Text.Encoding.UTF8, "application/json");

        await StreamProxyResponseAsync(client, req, httpContext, "application/json", ct);
        return Results.Empty;
    }

    private static async Task<IResult> GetMyLoginHistoryBffAsync(
        HttpContext httpContext,
        IHttpClientFactory httpClientFactory,
        CancellationToken ct)
    {
        var accessToken = httpContext.Request.Cookies[AuthCookieHelper.AccessCookieName];
        if (string.IsNullOrEmpty(accessToken)) return Results.Unauthorized();

        var client = httpClientFactory.CreateClient("InternalApi");
        using var req = new HttpRequestMessage(HttpMethod.Get, "/sessions/login-history");
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        await StreamProxyResponseAsync(client, req, httpContext, "application/json", ct);
        return Results.Empty;
    }

    private static async Task<IResult> ToggleSaveDiscussionAsync(string discussionId,
        SnakkApiClient apiClient, HttpContext httpContext, CancellationToken ct)
    {
        var result = await apiClient.ToggleSaveDiscussionAsync(discussionId, ct);
        if (result is null) return Results.StatusCode(503);
        return Results.Ok(new { isSaved = result.IsSaved });
    }

    private static async Task<IResult> ToggleSavePostAsync(string postId,
        SnakkApiClient apiClient, HttpContext httpContext, CancellationToken ct)
    {
        var result = await apiClient.ToggleSavePostAsync(postId, ct);
        if (result is null) return Results.StatusCode(503);
        return Results.Ok(new { isSaved = result.IsSaved });
    }

    private static async Task<IResult> GetSavedDiscussionIdsAsync(
        SnakkApiClient apiClient, HttpContext httpContext, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!IsAuthenticated(httpContext)) return Results.Ok(new { publicIds = Array.Empty<string>() });
        var ids = await apiClient.GetSavedDiscussionIdsAsync(ct);
        return Results.Ok(new { publicIds = ids });
    }

    private static async Task<IResult> GetSavedPostIdsAsync(
        SnakkApiClient apiClient, HttpContext httpContext, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!IsAuthenticated(httpContext)) return Results.Ok(new { publicIds = Array.Empty<string>() });
        var ids = await apiClient.GetSavedPostIdsAsync(ct);
        return Results.Ok(new { publicIds = ids });
    }

    private static async Task<IResult> GetSaveCountsAsync(
        SnakkApiClient apiClient, HttpContext httpContext, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!IsAuthenticated(httpContext)) return Results.Ok(new { discussionCount = 0, postCount = 0 });
        var result = await apiClient.GetSaveCountsAsync(ct);
        return Results.Ok(new { discussionCount = result?.DiscussionCount ?? 0, postCount = result?.PostCount ?? 0 });
    }

    private static async Task<IResult> ValidateHistoryIdsAsync(
        [FromBody] ValidateHistoryIdsRequest request,
        SnakkApiClient apiClient,
        HttpContext httpContext,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!IsAuthenticated(httpContext)) return Results.Ok(new { accessibleIds = Array.Empty<string>() });

        var ids = (request.Ids ?? [])
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Take(50)
            .ToList();

        var results = await Task.WhenAll(ids.Select(id => apiClient.GetDiscussionAsync(id, ct)));

        var accessibleIds = ids
            .Zip(results)
            .Where(x => x.Second != null)
            .Select(x => x.First)
            .ToList();

        return Results.Ok(new { accessibleIds });
    }

    // --- Social Links ---

    private static async Task<IResult> GetMySocialLinksBffAsync(IHttpClientFactory httpClientFactory, HttpContext httpContext, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var accessToken = httpContext.Request.Cookies[AuthCookieHelper.AccessCookieName];
        if (string.IsNullOrEmpty(accessToken)) return Results.Unauthorized();

        var client = httpClientFactory.CreateClient("InternalApi");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/me/social");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        await StreamProxyResponseAsync(client, request, httpContext, "application/json", ct);
        return Results.Empty;
    }

    private static async Task<IResult> UpdateMySocialLinksBffAsync(IHttpClientFactory httpClientFactory, HttpContext httpContext, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var accessToken = httpContext.Request.Cookies[AuthCookieHelper.AccessCookieName];
        if (string.IsNullOrEmpty(accessToken)) return Results.Unauthorized();

        var client = httpClientFactory.CreateClient("InternalApi");
        using var request = new HttpRequestMessage(HttpMethod.Put, "/me/social");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = new StreamContent(httpContext.Request.Body);
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

        await StreamProxyResponseAsync(client, request, httpContext, "application/json", ct);
        return Results.Empty;
    }

    private static async Task<IResult> GetUserSocialLinksBffAsync(string publicId, IHttpClientFactory httpClientFactory, HttpContext httpContext, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var client = httpClientFactory.CreateClient("InternalApi");
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/users/{Uri.EscapeDataString(publicId)}/social");

        await StreamProxyResponseAsync(client, request, httpContext, "application/json", ct);
        return Results.Empty;
    }

    private static async Task<IResult> GetActivitySparklineAsync(
        [FromQuery] string entityType,
        [FromQuery] string? entityId,
        [FromQuery] int days,
        SnakkApiClient apiClient,
        HttpContext httpContext,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        days = Math.Clamp(days, 1, 90);
        var result = await apiClient.GetActivitySparklineAsync(entityType, entityId, days, ct);
        if (result is null) return Results.NotFound();

        var now = DateTime.UtcNow;
        var secondsUntilNextHour = (60 - now.Minute) * 60 - now.Second;
        httpContext.Response.Headers.CacheControl = $"public, max-age={secondsUntilNextHour}";

        return Results.Ok(result.Days.Select(d => new
        {
            date            = d.Date,
            postCount       = d.PostCount,
            discussionCount = d.DiscussionCount
        }));
    }

    // â”€â”€ Direct Messages â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€

    private static bool IsDmEnabled(IConfiguration cfg)
        => cfg.GetValue<bool>("Features:PrivateMessagingEnabled");

    private static async Task<IResult> GetDmUnreadCountAsync(
        SnakkApiClient apiClient, HttpContext httpContext, IConfiguration configuration, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!IsDmEnabled(configuration)) return Results.NotFound();

        var result = await apiClient.GetDmUnreadCountAsync(ct);
        return Results.Ok(new Models.Bff.BffDmUnreadCountResponse(result?.Count ?? 0));
    }

    private static async Task<IResult> GetDmConversationsAsync(
        [FromQuery] int offset, [FromQuery] int pageSize,
        SnakkApiClient apiClient, HttpContext httpContext, IConfiguration configuration, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!IsDmEnabled(configuration)) return Results.NotFound();

        var result = await apiClient.GetDmConversationsAsync(offset, Math.Min(pageSize, 50), ct);
        if (result is null) return Results.Ok(new Models.Bff.BffDmConversationsResponse([], 0, 0, false));

        var items = result.Items.Select(MapConversation).ToList();
        return Results.Ok(new Models.Bff.BffDmConversationsResponse(items, result.Offset, result.PageSize, result.HasMoreItems));
    }

    private static async Task<IResult> GetDmConversationAsync(
        string conversationId, SnakkApiClient apiClient, HttpContext httpContext, IConfiguration configuration, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!IsDmEnabled(configuration)) return Results.NotFound();

        var result = await apiClient.GetDmConversationAsync(conversationId, ct);
        if (result is null || !result.Found || result.Conversation is null)
            return Results.NotFound();

        return Results.Ok(MapConversation(result.Conversation));
    }

    private static async Task<IResult> GetOrCreateDmConversationAsync(
        [FromBody] Models.Bff.BffDmCreateRequest body,
        SnakkApiClient apiClient, HttpContext httpContext, IConfiguration configuration, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!IsDmEnabled(configuration)) return Results.NotFound();
        if (string.IsNullOrWhiteSpace(body.RecipientPublicId)) return Results.BadRequest();

        var result = await apiClient.GetOrCreateDmConversationAsync(body.RecipientPublicId, ct);
        if (result is null || !result.Found || result.Conversation is null)
            return Results.BadRequest(new { error = "Could not create conversation" });

        return Results.Ok(MapConversation(result.Conversation));
    }

    private static async Task<IResult> GetDmMessagesAsync(
        string conversationId, [FromQuery] int offset, [FromQuery] int pageSize,
        SnakkApiClient apiClient, HttpContext httpContext, IConfiguration configuration, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!IsDmEnabled(configuration)) return Results.NotFound();

        var result = await apiClient.GetDmMessagesAsync(conversationId, offset, Math.Min(pageSize, 50), ct);
        if (result is null) return Results.Ok(new Models.Bff.BffDmMessagesResponse([], 0, 0, false));

        var items = result.Items.Select(m => new Models.Bff.BffDmMessageResponse(
            m.PublicId, m.SenderPublicId, m.Content,
            m.CreatedAt.ToDateTime().ToString("O"), m.IsMine)).ToList();

        return Results.Ok(new Models.Bff.BffDmMessagesResponse(items, result.Offset, result.PageSize, result.HasMoreItems));
    }

    private static async Task<IResult> SendDmMessageAsync(
        string conversationId, [FromBody] Models.Bff.BffDmSendRequest body,
        SnakkApiClient apiClient, HttpContext httpContext, IConfiguration configuration, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!IsDmEnabled(configuration)) return Results.NotFound();
        if (string.IsNullOrWhiteSpace(body.Content)) return Results.BadRequest();

        var result = await apiClient.SendDmMessageAsync(conversationId, body.Content, ct);
        if (result is null || !result.Success || result.Message is null)
            return Results.BadRequest(new { error = "Failed to send message" });

        var m = result.Message;
        return Results.Ok(new Models.Bff.BffDmMessageResponse(
            m.PublicId, m.SenderPublicId, m.Content,
            m.CreatedAt.ToDateTime().ToString("O"), m.IsMine));
    }

    private static async Task<IResult> MarkDmAsReadAsync(
        string conversationId, SnakkApiClient apiClient, HttpContext httpContext, IConfiguration configuration, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!IsDmEnabled(configuration)) return Results.NotFound();

        await apiClient.MarkDmAsReadAsync(conversationId, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> DeleteDmMessagesAsync(
        string conversationId, [FromBody] Models.Bff.BffDmDeleteMessagesRequest body,
        SnakkApiClient apiClient, HttpContext httpContext, IConfiguration configuration, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!IsDmEnabled(configuration)) return Results.NotFound();
        if (body.MessageIds is null || body.MessageIds.Count == 0) return Results.BadRequest();

        var success = await apiClient.DeleteDmMessagesAsync(conversationId, body.MessageIds, body.DeleteForAll, ct);
        return success ? Results.NoContent() : Results.BadRequest();
    }

    private static async Task<IResult> DeleteDmConversationAsync(
        string conversationId, [FromQuery] bool deleteForAll,
        SnakkApiClient apiClient, HttpContext httpContext, IConfiguration configuration, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!IsDmEnabled(configuration)) return Results.NotFound();

        var success = await apiClient.DeleteDmConversationAsync(conversationId, deleteForAll, ct);
        return success ? Results.NoContent() : Results.BadRequest();
    }

    private static async Task<IResult> PinDmConversationAsync(
        string conversationId, [FromBody] Models.Bff.BffDmPinRequest body,
        SnakkApiClient apiClient, HttpContext httpContext, IConfiguration configuration, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!IsDmEnabled(configuration)) return Results.NotFound();

        var success = await apiClient.PinDmConversationAsync(conversationId, body.IsPinned, ct);
        return success ? Results.NoContent() : Results.BadRequest();
    }

    private static async Task<IResult> DeleteMyAccountBffAsync(
        HttpContext httpContext,
        IHttpClientFactory httpClientFactory,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var accessToken = httpContext.Request.Cookies[AuthCookieHelper.AccessCookieName];
        if (string.IsNullOrEmpty(accessToken)) return Results.Unauthorized();

        var sudoToken = httpContext.Request.Cookies[AuthCookieHelper.SudoCookieName];
        if (string.IsNullOrWhiteSpace(sudoToken))
            return Results.Json(new { error = "Authentication required." }, statusCode: 403);

        using var reader = new System.IO.StreamReader(httpContext.Request.Body);
        var rawBody = await reader.ReadToEndAsync(ct);
        System.Text.Json.JsonElement clientJson;
        try { clientJson = System.Text.Json.JsonDocument.Parse(rawBody).RootElement; }
        catch { return Results.BadRequest(new { error = "Invalid request body." }); }

        var merged = new System.Collections.Generic.Dictionary<string, object?>
        {
            ["sudoToken"] = sudoToken
        };
        foreach (var prop in clientJson.EnumerateObject())
            merged[prop.Name] = prop.Value.Clone();

        var client = httpClientFactory.CreateClient("InternalApi");
        using var request = new HttpRequestMessage(HttpMethod.Delete, "/me/account");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = new StringContent(
            System.Text.Json.JsonSerializer.Serialize(merged),
            System.Text.Encoding.UTF8, "application/json");

        var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

        // Delete cookies based on response status headers — before streaming body
        if (response.IsSuccessStatusCode)
        {
            AuthCookieHelper.DeleteAuthCookies(httpContext);
            AuthCookieHelper.DeleteSudoCookie(httpContext);
        }

        httpContext.Response.StatusCode = (int)response.StatusCode;
        httpContext.Response.ContentType = "application/json";
        try { await response.Content.CopyToAsync(httpContext.Response.Body, ct); }
        finally { response.Dispose(); }
        return Results.Empty;
    }

    private static async Task<IResult> ExportMyDataBffAsync(
        HttpContext httpContext,
        IHttpClientFactory httpClientFactory,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var accessToken = httpContext.Request.Cookies[AuthCookieHelper.AccessCookieName];
        if (string.IsNullOrEmpty(accessToken)) return Results.Unauthorized();

        var client = httpClientFactory.CreateClient("InternalApi");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/me/data-export");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        // Send with ResponseHeadersRead so the export is never buffered in the BFF process.
        // Results.Stream owns the copy delegate; the response must stay alive until the delegate
        // completes — we dispose it inside the callback via a captured local.
        var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
        {
            var statusCode = (int)response.StatusCode;
            response.Dispose();
            return Results.StatusCode(statusCode);
        }

        var fileName = $"snakk-data-export-{DateTime.UtcNow:yyyy-MM-dd}.json";
        return Results.Stream(
            async body =>
            {
                using var r = response;
                await r.Content.CopyToAsync(body, ct);
            },
            contentType: "application/json",
            fileDownloadName: fileName);
    }

    private static Models.Bff.BffDmConversationResponse MapConversation(Snakk.Protos.Dm.ConversationInfo c) =>
        new(c.PublicId,
            new Models.Bff.BffDmUserResponse(
                c.OtherUser.PublicId,
                c.OtherUser.DisplayName,
                SnakkUrlHelper.UserAvatarThumbnail(
                    c.OtherUser.PublicId,
                    avatarThumbnailFileName: c.OtherUser.HasAvatarThumbnailFileName ? c.OtherUser.AvatarThumbnailFileName : null),
                c.OtherUser.HasSlug ? c.OtherUser.Slug : null),
            c.HasLastMessageExcerpt ? c.LastMessageExcerpt : null,
            c.LastMessageAt != null ? c.LastMessageAt.ToDateTime().ToString("O") : DateTime.UtcNow.ToString("O"),
            c.IsPinned);
}

public record ToggleReactionRequest(int Type);
public record PreviewMarkupRequest(string Content);
public record BffCreateReportRequest(string EntityType, string EntityId, string Reason, string? Description);
public record ReadStateUpdate(string DiscussionId, string PostId);
public record BatchUpdateReadStatesRequest(List<ReadStateUpdate> Updates);
public record UpdateProfileRequestDto(string DisplayName, string? Password = null, string? TurnstileToken = null);
public record UpdateProfileWithSudoDto(string DisplayName);
public record ValidateHistoryIdsRequest(IReadOnlyList<string>? Ids);
public record UpdatePreferencesRequestDto(bool? AutoFollowOnReply, string? Timezone = null, string? Bio = null, bool? AllowAdultContent = null, bool ClearAllowAdultContent = false, int? AdultPreviewImageMode = null, bool? HidePresence = null);
public record ReloginRequest(string Email, string Password);
public record RevokeAllOtherBffRequest(string ExcludeSessionId);

