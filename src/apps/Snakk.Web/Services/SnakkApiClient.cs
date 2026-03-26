namespace Snakk.Web.Services;

using System.Runtime.CompilerServices;
using Grpc.Core;
using Snakk.Web.Models;
using Google.Protobuf.WellKnownTypes;
using Snakk.Protos.Community;
using Snakk.Protos.Hub;
using Snakk.Protos.Space;
using Snakk.Protos.Discussion;
using Snakk.Protos.Post;
using Snakk.Protos.Follow;
using Snakk.Protos.Reaction;
using Snakk.Protos.Moderation;
using Snakk.Protos.Search;
using Snakk.Protos.Statistics;
using Snakk.Protos.User;
using Snakk.Protos.Markup;
using Snakk.Protos.Auth;

// Aliases for colliding types between Notification and ReadState namespaces
using NotifClient = Snakk.Protos.Notification.NotificationService.NotificationServiceClient;
using NotifGetRequest = Snakk.Protos.Notification.GetNotificationsRequest;
using NotifMarkReadRequest = Snakk.Protos.Notification.MarkAsReadRequest;
using NotifMarkAllReadRequest = Snakk.Protos.Notification.MarkAllAsReadRequest;
using NotifUnreadRequest = Snakk.Protos.Notification.GetUnreadCountRequest;
using PagedNotificationList = Snakk.Protos.Notification.PagedNotificationList;
using UnreadCountResponse = Snakk.Protos.Notification.UnreadCountResponse;
using ReadStateClient = Snakk.Protos.ReadState.ReadStateService.ReadStateServiceClient;
using ReadStateGetRequest = Snakk.Protos.ReadState.GetReadStateRequest;
using ReadStateMarkRequest = Snakk.Protos.ReadState.MarkAsReadRequest;
using ReadStateBatchRequest = Snakk.Protos.ReadState.BatchMarkAsReadRequest;
using ReadStateBatchItem = Snakk.Protos.ReadState.BatchReadStateItem;
using ReadStateInfo = Snakk.Protos.ReadState.ReadStateInfo;

public class SnakkApiClient(
    CommunityService.CommunityServiceClient communityClient,
    HubService.HubServiceClient hubClient,
    SpaceService.SpaceServiceClient spaceClient,
    DiscussionService.DiscussionServiceClient discussionClient,
    PostService.PostServiceClient postClient,
    FollowService.FollowServiceClient followClient,
    ReactionService.ReactionServiceClient reactionClient,
    NotifClient notificationClient,
    ModerationService.ModerationServiceClient moderationClient,
    SearchService.SearchServiceClient searchClient,
    StatisticsService.StatisticsServiceClient statisticsClient,
    UserService.UserServiceClient userClient,
    ReadStateClient readStateClient,
    MarkupService.MarkupServiceClient markupClient,
    AuthService.AuthServiceClient authClient,
    Snakk.Protos.Announcement.AnnouncementService.AnnouncementServiceClient announcementClient,
    ILogger<SnakkApiClient> logger)
{
    private void LogGrpcError(Exception ex, [CallerMemberName] string? caller = null)
        => logger.LogWarning(ex, "gRPC call failed in {Method}: {Status}",
            caller, ex is RpcException rpc ? rpc.StatusCode.ToString() : "N/A");

    // ==================== gRPC Result Helpers ====================

    private async Task<GrpcResult<T>> CallAsync<T>(
        Func<Task<T>> grpcCall,
        [CallerMemberName] string? caller = null)
    {
        try
        {
            var result = await grpcCall();
            return GrpcResult<T>.Ok(result);
        }
        catch (RpcException ex)
        {
            LogGrpcError(ex, caller);
            return GrpcResult<T>.FromRpcException(ex);
        }
    }

    private async Task<T?> CallOrDefaultAsync<T>(
        Func<Task<T>> grpcCall,
        T? fallback = default,
        [CallerMemberName] string? caller = null) where T : class
    {
        try { return await grpcCall(); }
        catch (RpcException ex) { LogGrpcError(ex, caller); return fallback; }
    }

    // ==================== Community ====================

    public virtual async Task<PagedCommunityList?> GetCommunitiesAsync(int offset = 0, int pageSize = 20)
    {
        try { return await communityClient.ListCommunitiesAsync(new ListCommunitiesRequest { Offset = offset, PageSize = pageSize }); }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<CommunityInfo?> GetCommunityBySlugAsync(string slug)
    {
        try { return await communityClient.GetCommunityBySlugAsync(new GetCommunityBySlugRequest { Slug = slug }); }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<CommunityInfo?> GetCommunityByDomainAsync(string domain)
    {
        try { return await communityClient.GetCommunityByDomainAsync(new GetCommunityByDomainRequest { Domain = domain }); }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<PagedHubList?> GetHubsByCommunityAsync(string communityId, int offset = 0, int pageSize = 20)
    {
        try
        {
            return await hubClient.ListHubsByCommunityAsync(new ListHubsByCommunityRequest
            {
                CommunityId = communityId,
                Offset = offset,
                PageSize = pageSize
            });
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    // ==================== Hub ====================

    public virtual async Task<PagedHubList?> GetHubsAsync(int offset = 0, int pageSize = 20)
    {
        try { return await hubClient.ListHubsAsync(new ListHubsRequest { Offset = offset, PageSize = pageSize }); }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<HubInfo?> GetHubBySlugAsync(string slug, string communitySlug)
    {
        try { return await hubClient.GetHubBySlugAsync(new GetHubBySlugRequest { Slug = slug, CommunitySlug = communitySlug }); }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    // ==================== Space ====================

    public virtual async Task<PagedSpaceByHubList?> GetSpacesByHubAsync(string hubId, int offset = 0, int pageSize = 20)
    {
        try
        {
            return await spaceClient.ListSpacesByHubAsync(new ListSpacesByHubRequest
            {
                HubId = hubId,
                Offset = offset,
                PageSize = pageSize
            });
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<SpaceInfo?> GetSpaceBySlugAsync(string slug, string hubSlug)
    {
        try { return await spaceClient.GetSpaceBySlugAsync(new GetSpaceBySlugRequest { Slug = slug, HubSlug = hubSlug }); }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<PagedDiscussionBySpaceList?> GetDiscussionsBySpaceAsync(
        string spaceId, int offset = 0, int pageSize = 20, int? typeFilter = null)
    {
        try
        {
            var request = new GetDiscussionsBySpaceRequest
            {
                SpaceId = spaceId,
                Offset = offset,
                PageSize = pageSize
            };

            if (typeFilter.HasValue)
                request.TypeFilter = typeFilter.Value;

            return await discussionClient.GetDiscussionsBySpaceAsync(request);
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<SpaceRulesResponse?> GetSpaceRulesAsync(string spaceId)
    {
        try { return await spaceClient.GetSpaceRulesAsync(new GetSpaceRulesRequest { SpaceId = spaceId }); }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<HubRulesResponse?> GetHubRulesAsync(string hubId)
    {
        try { return await hubClient.GetHubRulesAsync(new GetHubRulesRequest { HubId = hubId }); }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<CommunityRulesResponse?> GetCommunityRulesAsync(string communityId)
    {
        try { return await communityClient.GetCommunityRulesAsync(new GetCommunityRulesRequest { CommunityId = communityId }); }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<SiteRulesResponse?> GetSiteRulesAsync()
    {
        try { return await communityClient.GetSiteRulesAsync(new GetSiteRulesRequest()); }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    // ==================== Discussion ====================

    public virtual async Task<DiscussionInfo?> GetDiscussionAsync(string publicId)
    {
        try { return await discussionClient.GetDiscussionAsync(new GetDiscussionRequest { PublicId = publicId }); }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<DiscussionCreatedInfo?> CreateDiscussionAsync(
        string spaceId,
        string title,
        string content,
        int type = 0,
        IEnumerable<string>? tags = null,
        // Poll extension
        IEnumerable<string>? pollOptions = null,
        bool pollAllowMultiple = false,
        bool pollAllowChangeVote = false,
        DateTime? pollClosesAt = null,
        // Link extension
        string? linkUrl = null,
        // Debate extension
        IEnumerable<string>? debatePositions = null,
        bool debateAllowNeutral = false)
    {
        try
        {
            var request = new CreateDiscussionRequest { SpaceId = spaceId, Title = title, Content = content, Type = type };
            if (tags is not null) request.Tags.AddRange(tags);
            if (pollOptions is not null) request.PollOptions.AddRange(pollOptions);
            request.PollAllowMultiple = pollAllowMultiple;
            request.PollAllowChangeVote = pollAllowChangeVote;
            if (pollClosesAt.HasValue) request.PollClosesAt = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.SpecifyKind(pollClosesAt.Value, DateTimeKind.Utc));
            if (linkUrl is not null) request.LinkUrl = linkUrl;
            if (debatePositions is not null) request.DebatePositions.AddRange(debatePositions);
            request.DebateAllowNeutral = debateAllowNeutral;
            return await discussionClient.CreateDiscussionAsync(request);
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<PagedRecentDiscussionList?> GetRecentDiscussionsAsync(
        int offset = 0, int pageSize = 20, string? communityId = null, string? hubId = null)
    {
        try
        {
            var request = new GetRecentDiscussionsRequest { Offset = offset, PageSize = pageSize };
            if (communityId is not null) request.CommunityId = communityId;
            if (hubId is not null) request.HubId = hubId;

            return await discussionClient.GetRecentDiscussionsAsync(request);
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<TopActiveDiscussionsList?> GetTopActiveDiscussionsTodayAsync(
        string? hubId = null,
        string? spaceId = null,
        string? communityId = null)
    {
        try
        {
            var request = new GetTopActiveDiscussionsTodayRequest { Limit = 5 };
            if (hubId is not null) request.HubId = hubId;
            if (spaceId is not null) request.SpaceId = spaceId;
            if (communityId is not null) request.CommunityId = communityId;

            return await statisticsClient.GetTopActiveDiscussionsTodayAsync(request);
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<DiscussionPreviewInfo?> GetDiscussionPreviewAsync(string discussionId)
    {
        try { return await discussionClient.GetDiscussionPreviewAsync(new GetDiscussionPreviewRequest { DiscussionId = discussionId }); }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    // ==================== Post ====================

    public virtual async Task<PagedEnrichedPostList?> GetDiscussionPostsAsync(string discussionId, int offset = 0, int pageSize = 20)
    {
        try
        {
            return await postClient.GetPostsByDiscussionAsync(new GetPostsByDiscussionRequest
            {
                DiscussionId = discussionId,
                Offset = offset,
                PageSize = pageSize
            });
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<string?> CreatePostAsync(string discussionId, string content, string? replyToPostId = null)
    {
        try
        {
            var request = new CreatePostRequest { DiscussionId = discussionId, Content = content };
            if (replyToPostId is not null) request.ReplyToPostId = replyToPostId;
            var result = await postClient.CreatePostAsync(request);

            return result?.PublicId;
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<int> GetPostNumberAsync(string discussionId, string postId)
    {
        try
        {
            var result = await postClient.GetPostNumberAsync(new GetPostNumberRequest { DiscussionId = discussionId, PostId = postId });
            return result?.PostNumber ?? 1;
        }
        catch (RpcException ex) { LogGrpcError(ex); return 1; }
    }

    public virtual async Task<bool> EditPostAsync(string postId, string userId, string content)
    {
        try
        {
            await postClient.EditPostAsync(new EditPostRequest { PostId = postId, Content = content });
            return true;
        }
        catch (RpcException ex) { LogGrpcError(ex); return false; }
    }

    // ==================== Read State ====================

    public virtual async Task<ReadStateInfo?> GetReadStateAsync(string userId, string discussionId)
    {
        try { return await readStateClient.GetReadStateAsync(new ReadStateGetRequest { DiscussionId = discussionId }); }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task MarkDiscussionAsReadAsync(string discussionId, string userId, string postId)
    {
        try { await readStateClient.MarkAsReadAsync(new ReadStateMarkRequest { DiscussionId = discussionId, LastReadPostId = postId }); }
        catch (RpcException ex) { LogGrpcError(ex); }
    }

    public virtual async Task BatchUpdateReadStatesAsync(List<(string DiscussionId, string PostId)> updates)
    {
        try
        {
            var request = new ReadStateBatchRequest();
            foreach (var (discussionId, postId) in updates)
                request.Items.Add(new ReadStateBatchItem { DiscussionId = discussionId, LastReadPostId = postId });
            await readStateClient.BatchMarkAsReadAsync(request);
        }
        catch (RpcException ex) { LogGrpcError(ex); }
    }

    // ==================== Top Active / Trending ====================

    public virtual async Task<TopActiveSpacesList?> GetTopActiveSpacesTodayAsync(string? hubId = null, string? communityId = null)
    {
        try
        {
            var request = new GetTopActiveSpacesTodayRequest { Limit = 5 };
            if (hubId is not null) request.HubId = hubId;
            if (communityId is not null) request.CommunityId = communityId;

            return await statisticsClient.GetTopActiveSpacesTodayAsync(request);
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<TopContributorsList?> GetTopContributorsTodayAsync(
        string? hubId = null,
        string? spaceId = null,
        string? communityId = null)
    {
        try
        {
            var request = new GetTopContributorsTodayRequest { Limit = 5 };
            if (hubId is not null) request.HubId = hubId;
            if (spaceId is not null) request.SpaceId = spaceId;
            if (communityId is not null) request.CommunityId = communityId;

            return await statisticsClient.GetTopContributorsTodayAsync(request);
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    // Backward compatibility aliases
    public virtual Task<TopActiveDiscussionsList?> GetTopActiveDiscussionsAsync(string? communityId = null)
        => GetTopActiveDiscussionsTodayAsync(communityId: communityId);

    public virtual Task<TopActiveSpacesList?> GetTopActiveSpacesAsync(string? communityId = null)
        => GetTopActiveSpacesTodayAsync(communityId: communityId);

    public virtual Task<TopContributorsList?> GetTopContributorsAsync(string? communityId = null)
        => GetTopContributorsTodayAsync(communityId: communityId);

    // ==================== Stats ====================

    public virtual async Task<PlatformStats?> GetPlatformStatsAsync()
    {
        try { return await statisticsClient.GetPlatformStatsAsync(new GetPlatformStatsRequest()); }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<HubStats?> GetHubStatsAsync(string hubId)
    {
        try { return await hubClient.GetHubStatsAsync(new GetHubStatsRequest { PublicId = hubId }); }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<SpaceStats?> GetSpaceStatsAsync(string spaceId)
    {
        try { return await spaceClient.GetSpaceStatsAsync(new GetSpaceStatsRequest { PublicId = spaceId }); }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<CommunityStats?> GetCommunityStatsAsync(string communityId)
    {
        try { return await communityClient.GetCommunityStatsAsync(new GetCommunityStatsRequest { PublicId = communityId }); }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<DiscussionStats?> GetDiscussionStatsForPopupAsync(string publicId)
    {
        try { return await discussionClient.GetDiscussionStatsAsync(new GetDiscussionStatsRequest { PublicId = publicId }); }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    // Entity stats aliases
    public virtual Task<HubStats?> GetHubStatsForPopupAsync(string publicId) => GetHubStatsAsync(publicId);
    public virtual Task<SpaceStats?> GetSpaceStatsForPopupAsync(string publicId) => GetSpaceStatsAsync(publicId);
    public virtual Task<CommunityStats?> GetCommunityStatsForPopupAsync(string publicId) => GetCommunityStatsAsync(publicId);
    public virtual Task<UserStats?> GetUserStatsForPopupAsync(string publicId) => GetUserStatsAsync(publicId);

    // ==================== Group Access ====================

    public virtual async Task<CheckGroupAccessResponse?> CheckGroupAccessAsync(
        string communityPublicId,
        string? hubPublicId = null,
        string? spacePublicId = null)
    {
        try
        {
            var request = new CheckGroupAccessRequest { CommunityPublicId = communityPublicId };
            if (hubPublicId is not null) request.HubPublicId = hubPublicId;
            if (spacePublicId is not null) request.SpacePublicId = spacePublicId;
            return await communityClient.CheckGroupAccessAsync(request);
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    // ==================== Search ====================

    public virtual async Task<PagedDiscussionSearchResults?> SearchDiscussionsAsync(
        string? query = null, string? authorPublicId = null, string? spacePublicId = null,
        string? hubPublicId = null, int offset = 0, int pageSize = 20)
    {
        try
        {
            var request = new SearchDiscussionsRequest { Query = query ?? "", Offset = offset, PageSize = pageSize };
            if (authorPublicId is not null) request.AuthorId = authorPublicId;
            if (spacePublicId is not null) request.SpaceId = spacePublicId;
            if (hubPublicId is not null) request.HubId = hubPublicId;

            return await searchClient.SearchDiscussionsAsync(request);
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<PagedPostSearchResults?> SearchPostsAsync(
        string? query = null, string? authorPublicId = null, string? discussionPublicId = null,
        string? spacePublicId = null, int offset = 0, int pageSize = 20)
    {
        try
        {
            var request = new SearchPostsRequest { Query = query ?? "", Offset = offset, PageSize = pageSize };
            if (authorPublicId is not null) request.AuthorId = authorPublicId;
            if (discussionPublicId is not null) request.DiscussionId = discussionPublicId;
            if (spacePublicId is not null) request.SpaceId = spacePublicId;

            return await searchClient.SearchPostsAsync(request);
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<UserProfileInfo?> GetUserProfileAsync(string publicId)
    {
        try { return await userClient.GetUserProfileAsync(new GetUserProfileRequest { PublicId = publicId }); }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    // ==================== Auth ====================

    public virtual async Task<AuthStatusResponse?> GetAuthStatusAsync()
    {
        try { return await authClient.GetAuthStatusAsync(new GetAuthStatusRequest()); }
        catch (RpcException ex) { LogGrpcError(ex); return new AuthStatusResponse { IsAuthenticated = false }; }
    }

    public virtual async Task<CurrentUserResponse?> GetCurrentUserAsync()
    {
        try
        {
            var result = await authClient.GetCurrentUserAsync(new GetCurrentUserRequest());
            return result.IsAuthenticated ? result : null;
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<DisplayNameHistoryResponse?> GetDisplayNameHistoryAsync()
    {
        try { return await authClient.GetDisplayNameHistoryAsync(new GetDisplayNameHistoryRequest()); }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<UpdateProfileResponse?> UpdateProfileAsync(string displayName, string? password = null, string? turnstileToken = null)
    {
        try
        {
            var request = new UpdateProfileRequest { DisplayName = displayName };
            if (password is not null) request.Password = password;
            if (turnstileToken is not null) request.TurnstileToken = turnstileToken;
            return await authClient.UpdateProfileAsync(request);
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<bool> UpdatePreferencesAsync(bool? preferEndlessScroll = null, bool? autoFollowOnReply = null, string? timezone = null)
    {
        try
        {
            var request = new UpdatePreferencesRequest();
            if (preferEndlessScroll.HasValue) request.PreferEndlessScroll = preferEndlessScroll.Value;
            if (autoFollowOnReply.HasValue) request.AutoFollowOnReply = autoFollowOnReply.Value;
            if (timezone is not null) request.Timezone = timezone;
            await authClient.UpdatePreferencesAsync(request);

            return true;
        }
        catch (RpcException ex) { LogGrpcError(ex); return false; }
    }

    public virtual async Task LogoutAsync()
    {
        try { await authClient.LogoutAsync(new LogoutRequest()); }
        catch (RpcException ex) { LogGrpcError(ex); }
    }

    public virtual async Task<string?> GetSiteTimezoneAsync()
    {
        try
        {
            var response = await authClient.GetPublicSettingsAsync(new GetPublicSettingsRequest());
            return string.IsNullOrEmpty(response.Timezone) ? null : response.Timezone;
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<bool> IsHealthyAsync()
    {
        try
        {
            await statisticsClient.GetPlatformStatsAsync(new GetPlatformStatsRequest());
            return true;
        }
        catch (Exception ex) { LogGrpcError(ex); return false; }
    }

    // ==================== Follow ====================

    // Space follow
    public virtual async Task<SpaceFollowStatusResponse?> GetSpaceFollowStatusAsync(string spaceId)
    {
        try { return await followClient.GetSpaceFollowStatusAsync(new GetSpaceFollowStatusRequest { SpaceId = spaceId }); }
        catch (RpcException ex) { LogGrpcError(ex); return new SpaceFollowStatusResponse { IsFollowing = false }; }
    }

    public virtual async Task<SpaceFollowToggleResponse?> ToggleSpaceFollowAsync(string spaceId, string? level)
    {
        try
        {
            var request = new ToggleSpaceFollowRequest { SpaceId = spaceId };
            if (level is not null) request.Level = level;

            return await followClient.ToggleSpaceFollowAsync(request);
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<FollowLevelResponse?> SetSpaceFollowLevelAsync(string spaceId, string level)
    {
        try { return await followClient.SetSpaceFollowLevelAsync(new SetSpaceFollowLevelRequest { SpaceId = spaceId, Level = level }); }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    // Discussion follow
    public virtual async Task<FollowToggleResponse?> GetDiscussionFollowStatusAsync(string discussionId)
    {
        try { return await followClient.GetDiscussionFollowStatusAsync(new GetDiscussionFollowStatusRequest { DiscussionId = discussionId }); }
        catch (RpcException ex) { LogGrpcError(ex); return new FollowToggleResponse { IsFollowing = false }; }
    }

    public virtual async Task<FollowToggleResponse?> ToggleDiscussionFollowAsync(string discussionId)
    {
        try { return await followClient.ToggleDiscussionFollowAsync(new ToggleDiscussionFollowRequest { DiscussionId = discussionId }); }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    // User follow
    public virtual async Task<FollowToggleResponse?> GetUserFollowStatusAsync(string userId, string currentUserId)
    {
        try { return await followClient.GetUserFollowStatusAsync(new GetUserFollowStatusRequest { UserId = userId }); }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<FollowToggleResponse?> ToggleUserFollowAsync(string userId)
    {
        try { return await followClient.ToggleUserFollowAsync(new ToggleUserFollowRequest { UserId = userId }); }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    // Follow lists
    public virtual async Task<List<string>> GetFollowedSpacesAsync()
    {
        try
        {
            var result = await followClient.GetFollowedSpacesAsync(new GetFollowedSpacesRequest());
            return result?.PublicIds?.ToList() ?? [];
        }
        catch (RpcException ex) { LogGrpcError(ex); return []; }
    }

    public virtual async Task<List<string>> GetFollowedDiscussionsAsync()
    {
        try
        {
            var result = await followClient.GetFollowedDiscussionsAsync(new GetFollowedDiscussionsRequest());
            return result?.PublicIds?.ToList() ?? [];
        }
        catch (RpcException ex) { LogGrpcError(ex); return []; }
    }

    public virtual async Task<List<string>> GetFollowedUsersAsync()
    {
        try
        {
            var result = await followClient.GetFollowedUsersAsync(new GetFollowedUsersRequest());
            return result?.PublicIds?.ToList() ?? [];
        }
        catch (RpcException ex) { LogGrpcError(ex); return []; }
    }

    // ==================== Reactions ====================

    public virtual async Task<Dictionary<string, int>?> GetPostReactionsAsync(string postId)
    {
        try
        {
            var result = await reactionClient.GetReactionCountsAsync(new GetReactionCountsRequest { PostId = postId });
            return result?.Counts?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value) ?? new Dictionary<string, int>();
        }
        catch (RpcException ex) { LogGrpcError(ex); return new Dictionary<string, int>(); }
    }

    public virtual async Task<List<string>?> GetMyPostReactionsAsync(string postId)
    {
        try
        {
            var result = await reactionClient.GetMyReactionsAsync(new GetMyReactionsRequest { PostId = postId });
            return result?.Reactions?.ToList() ?? [];
        }
        catch (RpcException ex) { LogGrpcError(ex); return []; }
    }

    public virtual async Task TogglePostReactionAsync(string postId, int type)
    {
        try { await reactionClient.ToggleReactionAsync(new ToggleReactionRequest { PostId = postId, ReactionType = type.ToString() }); }
        catch (RpcException ex) { LogGrpcError(ex); }
    }

    // ==================== Notifications ====================

    public virtual async Task<PagedNotificationList?> GetNotificationsAsync(int offset = 0, int pageSize = 10)
    {
        try { return await notificationClient.GetNotificationsAsync(new NotifGetRequest { Offset = offset, PageSize = pageSize }); }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<UnreadCountResponse?> GetUnreadNotificationCountAsync()
    {
        try { return await notificationClient.GetUnreadCountAsync(new NotifUnreadRequest()); }
        catch (RpcException ex) { LogGrpcError(ex); return new UnreadCountResponse { Count = 0 }; }
    }

    public virtual async Task MarkNotificationAsReadAsync(string notificationId)
    {
        try { await notificationClient.MarkAsReadAsync(new NotifMarkReadRequest { NotificationId = notificationId }); }
        catch (RpcException ex) { LogGrpcError(ex); }
    }

    public virtual async Task MarkAllNotificationsAsReadAsync()
    {
        try { await notificationClient.MarkAllAsReadAsync(new NotifMarkAllReadRequest()); }
        catch (RpcException ex) { LogGrpcError(ex); }
    }

    // ==================== Markup ====================

    public virtual async Task<string?> PreviewMarkupAsync(string content)
    {
        try
        {
            var result = await markupClient.PreviewAsync(new PreviewMarkupRequest { Content = content });
            return result?.Html;
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    // ==================== User ====================

    public virtual async Task<UserStats?> GetUserStatsAsync(string userId)
    {
        try { return await statisticsClient.GetUserStatsAsync(new GetUserStatsRequest { PublicId = userId }); }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<UserActivityHistory?> GetUserActivityHistoryAsync(string userId, int days = 30)
    {
        try { return await statisticsClient.GetUserActivityHistoryAsync(new GetUserActivityHistoryRequest { PublicId = userId, Days = days }); }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    // Endless scroll (alias for GetDiscussionsBySpaceAsync)
    public virtual Task<PagedDiscussionBySpaceList?> GetSpaceDiscussionsAsync(string spaceId, int offset, int pageSize)
        => GetDiscussionsBySpaceAsync(spaceId, offset, pageSize);

    // ==================== Moderation ====================

    public virtual async Task<bool> CanModerateAsync(string? communityId = null, string? hubId = null, string? spaceId = null)
    {
        try
        {
            var request = new CanModerateRequest();
            if (communityId is not null) request.CommunityId = communityId;
            if (hubId is not null) request.HubId = hubId;
            if (spaceId is not null) request.SpaceId = spaceId;
            var result = await moderationClient.CanModerateAsync(request);

            return result.CanModerate;
        }
        catch (RpcException ex) { LogGrpcError(ex); return false; }
    }

    public virtual async Task<bool> CanAdministerAsync(string? communityId = null, string? hubId = null, string? spaceId = null)
    {
        try
        {
            var request = new CanAdministerRequest();
            if (communityId is not null) request.CommunityId = communityId;
            if (hubId is not null) request.HubId = hubId;
            if (spaceId is not null) request.SpaceId = spaceId;
            var result = await moderationClient.CanAdministerAsync(request);

            return result.CanAdminister;
        }
        catch (RpcException ex) { LogGrpcError(ex); return false; }
    }

    // Role management — returns local DTOs mapped from proto
    public virtual async Task<IEnumerable<UserRoleDto>?> GetMyRolesAsync()
    {
        try
        {
            var result = await moderationClient.GetMyRolesAsync(new GetMyRolesRequest());
            return result.Items.Select(MapRoleInfo);
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<IEnumerable<UserRoleDto>?> GetUserRolesAsync(string userId)
    {
        try
        {
            var result = await moderationClient.GetUserRolesAsync(new GetUserRolesRequest { UserId = userId });
            return result.Items.Select(MapRoleInfo);
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<IEnumerable<UserRoleDto>?> GetRolesForCommunityAsync(string communityId)
    {
        try
        {
            var result = await moderationClient.GetRolesForCommunityAsync(new GetRolesForScopeRequest { ScopeId = communityId });
            return result.Items.Select(MapRoleInfo);
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<IEnumerable<UserRoleDto>?> GetRolesForHubAsync(string hubId)
    {
        try
        {
            var result = await moderationClient.GetRolesForHubAsync(new GetRolesForScopeRequest { ScopeId = hubId });
            return result.Items.Select(MapRoleInfo);
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<IEnumerable<UserRoleDto>?> GetRolesForSpaceAsync(string spaceId)
    {
        try
        {
            var result = await moderationClient.GetRolesForSpaceAsync(new GetRolesForScopeRequest { ScopeId = spaceId });
            return result.Items.Select(MapRoleInfo);
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<UserRoleDto?> AssignRoleAsync(Snakk.Web.Models.AssignRoleRequest request)
    {
        try
        {
            var grpcRequest = new Snakk.Protos.Moderation.AssignRoleRequest
            {
                UserId = request.TargetUserId,
                Role = request.RoleType
            };
            if (request.CommunityId is not null) grpcRequest.CommunityId = request.CommunityId;
            if (request.HubId is not null) grpcRequest.HubId = request.HubId;
            if (request.SpaceId is not null) grpcRequest.SpaceId = request.SpaceId;
            var result = await moderationClient.AssignRoleAsync(grpcRequest);

            return MapRoleInfo(result);
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<bool> RevokeRoleAsync(string roleId)
    {
        try
        {
            var result = await moderationClient.RevokeRoleAsync(new RevokeRoleRequest { RoleId = roleId });
            return result.Success;
        }
        catch (RpcException ex) { LogGrpcError(ex); return false; }
    }

    // Public moderator list
    public virtual async Task<GetModeratorsResponse?> GetModeratorsAsync(string scopeType, string scopePublicId)
    {
        try
        {
            return await moderationClient.GetModeratorsAsync(
                new GetModeratorsRequest { ScopeType = scopeType, ScopePublicId = scopePublicId });
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    // Ban management
    public virtual async Task<IEnumerable<UserBanDto>?> GetUserBansAsync(string userId)
    {
        try
        {
            var result = await moderationClient.GetUserBansAsync(new GetUserBansRequest { UserId = userId });
            return result.Items.Select(MapBanInfo);
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<BanCheckResult?> CheckUserBanAsync(
        string userId,
        string? communityId = null,
        string? hubId = null,
        string? spaceId = null)
    {
        try
        {
            var request = new CheckUserBanRequest { UserId = userId };
            if (communityId is not null) request.CommunityId = communityId;
            if (hubId is not null) request.HubId = hubId;
            if (spaceId is not null) request.SpaceId = spaceId;
            var result = await moderationClient.CheckUserBanAsync(request);

            return new BanCheckResult(result.IsBanned, result.Ban is not null ? MapBanInfo(result.Ban) : null);
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<UserBanDto?> BanUserAsync(Snakk.Web.Models.BanUserRequest request)
    {
        try
        {
            var grpcRequest = new Snakk.Protos.Moderation.BanUserRequest
            {
                UserId = request.TargetUserId,
                BanType = request.BanType
            };
            if (request.CommunityId is not null) grpcRequest.CommunityId = request.CommunityId;
            if (request.HubId is not null) grpcRequest.HubId = request.HubId;
            if (request.SpaceId is not null) grpcRequest.SpaceId = request.SpaceId;
            if (request.Reason is not null) grpcRequest.Reason = request.Reason;
            if (request.ExpiresAt.HasValue)
                grpcRequest.ExpiresAt = Timestamp.FromDateTime(DateTime.SpecifyKind(request.ExpiresAt.Value, DateTimeKind.Utc));
            var result = await moderationClient.BanUserAsync(grpcRequest);

            return MapBanInfo(result);
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<bool> UnbanUserAsync(string banId)
    {
        try
        {
            var result = await moderationClient.UnbanUserAsync(new UnbanUserRequest { BanId = banId });
            return result.Success;
        }
        catch (RpcException ex) { LogGrpcError(ex); return false; }
    }

    // Report management
    public virtual async Task<int> GetPendingReportCountAsync()
    {
        try
        {
            var result = await moderationClient.GetPendingReportCountAsync(new GetPendingReportCountRequest());
            return result.Count;
        }
        catch (RpcException ex) { LogGrpcError(ex); return 0; }
    }

    public virtual async Task<PagedResult<ReportListDto>?> GetReportsAsync(string? status = null, int offset = 0, int pageSize = 20)
    {
        try
        {
            var request = new GetReportsRequest { Offset = offset, PageSize = pageSize };
            if (status is not null && int.TryParse(status, out var statusId)) request.StatusId = statusId;
            var result = await moderationClient.GetReportsAsync(request);

            return new PagedResult<ReportListDto>(
                result.Items.Select(MapReportListItem),
                result.Offset,
                result.PageSize,
                result.Total > result.Offset + result.PageSize);
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<ReportDetailDto?> GetReportDetailAsync(string reportId)
    {
        try
        {
            var r = await moderationClient.GetReportDetailAsync(new GetReportDetailRequest { ReportId = reportId });
            return new ReportDetailDto(
                r.PublicId, r.Status,
                r.ReporterUserPublicId, r.ReporterUserDisplayName,
                r.ReportedPostPublicId, r.ReportedPostContent,
                r.ReportedDiscussionPublicId, r.ReportedDiscussionTitle,
                r.ReportedUserPublicId, r.ReportedUserDisplayName,
                r.ReasonName, r.ReasonDescription, r.Details,
                r.CreatedAt.ToDateTime(),
                r.ResolvedAt?.ToDateTime(),
                r.ResolvedByUserPublicId, r.ResolvedByUserDisplayName,
                r.ResolutionNote,
                r.SpacePublicId, r.SpaceName,
                r.HubPublicId, r.HubName,
                r.CommunityPublicId, r.CommunityName,
                r.Comments.Select(c => new ReportCommentDto(
                    c.PublicId, c.AuthorUserPublicId, c.AuthorUserDisplayName,
                    c.Content, c.CreatedAt.ToDateTime(), c.EditedAt?.ToDateTime())));
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<ReportDto?> CreateReportAsync(Snakk.Web.Models.CreateReportRequest request)
    {
        try
        {
            var grpcRequest = new Snakk.Protos.Moderation.CreateReportRequest();
            if (request.ReportedPostId is not null) grpcRequest.PostId = request.ReportedPostId;
            if (request.ReportedDiscussionId is not null) grpcRequest.DiscussionId = request.ReportedDiscussionId;
            if (request.ReportedUserId is not null) grpcRequest.UserId = request.ReportedUserId;
            if (request.ReasonId is not null) grpcRequest.ReasonId = request.ReasonId;
            if (request.Details is not null) grpcRequest.Details = request.Details;
            var result = await moderationClient.CreateReportAsync(grpcRequest);

            return new ReportDto(result.PublicId, result.Status, "", null, null, null, null, null,
                result.CreatedAt.ToDateTime(), null, null, null);
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<bool> ResolveReportAsync(string reportId, Snakk.Web.Models.ResolveReportRequest request)
    {
        try
        {
            var grpcRequest = new Snakk.Protos.Moderation.ResolveReportRequest
            {
                ReportId = reportId,
                Dismiss = request.Dismiss
            };
            if (request.ResolutionNote is not null) grpcRequest.ResolutionNote = request.ResolutionNote;
            var result = await moderationClient.ResolveReportAsync(grpcRequest);

            return result.Success;
        }
        catch (RpcException ex) { LogGrpcError(ex); return false; }
    }

    public virtual async Task<ReportCommentDto?> AddReportCommentAsync(string reportId, Snakk.Web.Models.AddReportCommentRequest request)
    {
        try
        {
            var result = await moderationClient.AddReportCommentAsync(
                new Snakk.Protos.Moderation.AddReportCommentRequest { ReportId = reportId, Content = request.Content });
            return new ReportCommentDto(
                result.PublicId, result.AuthorUserPublicId, result.AuthorUserDisplayName,
                result.Content, result.CreatedAt.ToDateTime(), result.EditedAt?.ToDateTime());
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<IEnumerable<ReportReasonDto>?> GetReportReasonsAsync(
        string? communityId = null,
        string? hubId = null,
        string? spaceId = null)
    {
        try
        {
            var request = new GetReportReasonsRequest();
            if (spaceId is not null) request.SpaceId = spaceId;
            var result = await moderationClient.GetReportReasonsAsync(request);

            return result.Items.Select(r => new ReportReasonDto(
                r.PublicId, r.Name, r.Description,
                r.CommunityPublicId, r.HubPublicId, r.SpacePublicId, r.DisplayOrder));
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    // Moderation log
    public virtual async Task<PagedResult<ModerationLogDto>?> GetModerationLogsAsync(
        string? communityId = null, string? hubId = null, string? spaceId = null,
        int offset = 0, int pageSize = 20)
    {
        try
        {
            var request = new GetModerationLogsRequest { Offset = offset, PageSize = pageSize };
            if (communityId is not null) request.CommunityId = communityId;
            if (hubId is not null) request.HubId = hubId;
            if (spaceId is not null) request.SpaceId = spaceId;
            var result = await moderationClient.GetModerationLogsAsync(request);

            return new PagedResult<ModerationLogDto>(
                result.Items.Select(MapModerationLogItem),
                result.Offset,
                result.PageSize,
                result.Total > result.Offset + result.PageSize);
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    // Content moderation
    public virtual async Task<bool> DeletePostAsync(string postId, string? reason = null)
    {
        try
        {
            var request = new DeletePostRequest { PostId = postId };
            if (reason is not null) request.Reason = reason;
            var result = await moderationClient.DeletePostAsync(request);

            return result.Success;
        }
        catch (RpcException ex) { LogGrpcError(ex); return false; }
    }

    public virtual async Task<bool> DeleteDiscussionAsync(string discussionId, string? reason = null)
    {
        try
        {
            var request = new DeleteDiscussionRequest { DiscussionId = discussionId };
            if (reason is not null) request.Reason = reason;
            var result = await moderationClient.DeleteDiscussionAsync(request);

            return result.Success;
        }
        catch (RpcException ex) { LogGrpcError(ex); return false; }
    }

    public virtual async Task<bool> LockDiscussionAsync(string discussionId, string? reason = null)
    {
        try
        {
            var request = new LockDiscussionRequest { DiscussionId = discussionId };
            if (reason is not null) request.Reason = reason;
            var result = await moderationClient.LockDiscussionAsync(request);

            return result.Success;
        }
        catch (RpcException ex) { LogGrpcError(ex); return false; }
    }

    public virtual async Task<bool> UnlockDiscussionAsync(string discussionId)
    {
        try
        {
            var result = await moderationClient.UnlockDiscussionAsync(new UnlockDiscussionRequest { DiscussionId = discussionId });
            return result.Success;
        }
        catch (RpcException ex) { LogGrpcError(ex); return false; }
    }

    // ==================== GrpcResult<T> Overloads ====================
    // These return typed results with error differentiation.
    // Callers can distinguish NotFound vs Unauthenticated vs ServerError.

    public virtual Task<GrpcResult<CommunityInfo>> GetCommunityBySlugResultAsync(string slug) =>
        CallAsync(() => communityClient.GetCommunityBySlugAsync(
            new GetCommunityBySlugRequest { Slug = slug }).ResponseAsync);

    public virtual Task<GrpcResult<HubInfo>> GetHubBySlugResultAsync(string slug, string communitySlug) =>
        CallAsync(() => hubClient.GetHubBySlugAsync(
            new GetHubBySlugRequest { Slug = slug, CommunitySlug = communitySlug }).ResponseAsync);

    public virtual Task<GrpcResult<SpaceInfo>> GetSpaceBySlugResultAsync(string slug, string hubSlug) =>
        CallAsync(() => spaceClient.GetSpaceBySlugAsync(
            new GetSpaceBySlugRequest { Slug = slug, HubSlug = hubSlug }).ResponseAsync);

    public virtual Task<GrpcResult<DiscussionInfo>> GetDiscussionResultAsync(string publicId) =>
        CallAsync(() => discussionClient.GetDiscussionAsync(
            new GetDiscussionRequest { PublicId = publicId }).ResponseAsync);

    public virtual Task<GrpcResult<UserProfileInfo>> GetUserProfileResultAsync(string publicId) =>
        CallAsync(() => userClient.GetUserProfileAsync(
            new GetUserProfileRequest { PublicId = publicId }).ResponseAsync);

    public virtual Task<GrpcResult<SpaceFollowToggleResponse>> ToggleSpaceFollowResultAsync(string spaceId, string? level)
    {
        var request = new ToggleSpaceFollowRequest { SpaceId = spaceId };
        if (level is not null) request.Level = level;
        return CallAsync(() => followClient.ToggleSpaceFollowAsync(request).ResponseAsync);
    }

    public virtual Task<GrpcResult<FollowToggleResponse>> ToggleDiscussionFollowResultAsync(string discussionId) =>
        CallAsync(() => followClient.ToggleDiscussionFollowAsync(
            new ToggleDiscussionFollowRequest { DiscussionId = discussionId }).ResponseAsync);

    public virtual Task<GrpcResult<FollowToggleResponse>> ToggleUserFollowResultAsync(string userId) =>
        CallAsync(() => followClient.ToggleUserFollowAsync(
            new ToggleUserFollowRequest { UserId = userId }).ResponseAsync);

    public virtual Task<GrpcResult<PostCreatedInfo>> CreatePostResultAsync(string discussionId, string content, string? replyToPostId = null)
    {
        var request = new CreatePostRequest { DiscussionId = discussionId, Content = content };
        if (replyToPostId is not null) request.ReplyToPostId = replyToPostId;
        return CallAsync(() => postClient.CreatePostAsync(request).ResponseAsync);
    }

    public virtual Task<GrpcResult<EditPostResponse>> EditPostResultAsync(string postId, string content) =>
        CallAsync(() => postClient.EditPostAsync(
            new EditPostRequest { PostId = postId, Content = content }).ResponseAsync);

    // ==================== Private mapping helpers ====================

    private static UserRoleDto MapRoleInfo(RoleInfo r) => new(
        r.PublicId, r.UserPublicId, r.UserDisplayName, r.Role,
        r.CommunityId, r.CommunityName,
        r.HubId, r.HubName,
        r.SpaceId, r.SpaceName,
        r.AssignedByUserPublicId, r.AssignedByUserDisplayName,
        r.AssignedAt.ToDateTime(),
        r.RevokedAt?.ToDateTime());

    private static UserBanDto MapBanInfo(BanInfo b) => new(
        b.PublicId, b.UserPublicId, b.UserDisplayName, b.BanType,
        b.CommunityId, b.CommunityName,
        b.HubId, b.HubName,
        b.SpaceId, b.SpaceName,
        b.Reason, b.BannedAt.ToDateTime(), b.ExpiresAt?.ToDateTime(),
        b.BannedByUserPublicId, b.BannedByUserDisplayName,
        b.UnbannedAt?.ToDateTime(),
        b.UnbannedByUserPublicId, b.UnbannedByUserDisplayName);

    private static ReportListDto MapReportListItem(ReportListItem r) => new(
        r.PublicId, r.Status,
        r.ReporterUserPublicId, r.ReporterUserDisplayName,
        r.ReportedPostPublicId, r.ReportedPostContentSnippet,
        r.ReportedDiscussionPublicId, r.ReportedDiscussionTitle,
        r.ReportedUserPublicId, r.ReportedUserDisplayName,
        r.ReasonName, r.Details,
        r.CreatedAt.ToDateTime(), r.ResolvedAt?.ToDateTime(),
        r.ResolvedByUserPublicId, r.ResolvedByUserDisplayName,
        r.ResolutionNote,
        r.SpacePublicId, r.SpaceName,
        r.HubPublicId, r.HubName,
        r.CommunityPublicId, r.CommunityName,
        r.CommentCount);

    private static ModerationLogDto MapModerationLogItem(ModerationLogItem l) => new(
        l.PublicId, l.ActorUserPublicId, l.ActorUserDisplayName,
        l.Action,
        l.TargetPostPublicId, l.TargetDiscussionPublicId, l.TargetDiscussionTitle,
        l.TargetUserPublicId, l.TargetUserDisplayName,
        l.CommunityPublicId, l.CommunityName,
        l.HubPublicId, l.HubName,
        l.SpacePublicId, l.SpaceName,
        l.Details, l.Reason,
        l.CreatedAt.ToDateTime());

    // ==================== Announcements ====================

    public async Task<Snakk.Protos.Announcement.AnnouncementList?> GetActiveAnnouncementsForCommunityAsync(string communityPublicId)
    {
        try
        {
            return await announcementClient.GetActiveForCommunityAsync(
                new Snakk.Protos.Announcement.GetActiveAnnouncementsRequest { EntityId = communityPublicId });
        }
        catch (RpcException ex)
        {
            LogGrpcError(ex);
            return null;
        }
    }

    public async Task<Snakk.Protos.Announcement.AnnouncementList?> GetActiveAnnouncementsForHubAsync(string hubPublicId)
    {
        try
        {
            return await announcementClient.GetActiveForHubAsync(
                new Snakk.Protos.Announcement.GetActiveAnnouncementsRequest { EntityId = hubPublicId });
        }
        catch (RpcException ex)
        {
            LogGrpcError(ex);
            return null;
        }
    }

    public async Task<Snakk.Protos.Announcement.AnnouncementList?> GetActiveAnnouncementsForSpaceAsync(string spacePublicId)
    {
        try
        {
            return await announcementClient.GetActiveForSpaceAsync(
                new Snakk.Protos.Announcement.GetActiveAnnouncementsRequest { EntityId = spacePublicId });
        }
        catch (RpcException ex)
        {
            LogGrpcError(ex);
            return null;
        }
    }
}

// Paged result for moderation responses
public record PagedResult<T>(
    IEnumerable<T> Items,
    int Offset,
    int PageSize,
    bool HasMoreItems);
