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
using Snakk.Protos.Save;

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
    Snakk.Protos.Banner.BannerService.BannerServiceClient bannerClient,
    Snakk.Protos.Consent.ConsentService.ConsentServiceClient consentClient,
    SaveService.SaveServiceClient saveClient,
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

    public virtual async Task<PagedCommunityList?> GetCommunitiesAsync(int offset = 0, int pageSize = 20, CancellationToken ct = default)
    {
        try { return await communityClient.ListCommunitiesAsync(new ListCommunitiesRequest { Offset = offset, PageSize = pageSize }, cancellationToken: ct); }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<CommunityInfo?> GetCommunityBySlugAsync(string slug, CancellationToken ct = default)
    {
        try { return await communityClient.GetCommunityBySlugAsync(new GetCommunityBySlugRequest { Slug = slug }, cancellationToken: ct); }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<CommunityInfo?> GetCommunityByDomainAsync(string domain, CancellationToken ct = default)
    {
        try { return await communityClient.GetCommunityByDomainAsync(new GetCommunityByDomainRequest { Domain = domain }, cancellationToken: ct); }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound) { return null; }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<CommunityInfo?> GetCommunityAsync(string publicId, CancellationToken ct = default)
    {
        try { return await communityClient.GetCommunityAsync(new GetCommunityRequest { PublicId = publicId }, cancellationToken: ct); }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<PagedHubList?> GetHubsByCommunityAsync(string communityId, int offset = 0, int pageSize = 20, CancellationToken ct = default)
    {
        try
        {
            return await hubClient.ListHubsByCommunityAsync(new ListHubsByCommunityRequest
            {
                CommunityId = communityId,
                Offset = offset,
                PageSize = pageSize
            }, cancellationToken: ct);
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    // ==================== Hub ====================

    public virtual async Task<PagedHubList?> GetHubsAsync(int offset = 0, int pageSize = 20, CancellationToken ct = default)
    {
        try { return await hubClient.ListHubsAsync(new ListHubsRequest { Offset = offset, PageSize = pageSize }, cancellationToken: ct); }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<HubInfo?> GetHubBySlugAsync(string slug, string communitySlug, CancellationToken ct = default)
    {
        try { return await hubClient.GetHubBySlugAsync(new GetHubBySlugRequest { Slug = slug, CommunitySlug = communitySlug }, cancellationToken: ct); }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<HubInfo?> GetHubAsync(string publicId, CancellationToken ct = default)
    {
        try { return await hubClient.GetHubAsync(new GetHubRequest { PublicId = publicId }, cancellationToken: ct); }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    // ==================== Space ====================

    public virtual async Task<PagedSpaceByHubList?> GetSpacesByHubAsync(string hubId, int offset = 0, int pageSize = 20, CancellationToken ct = default)
    {
        try
        {
            return await spaceClient.ListSpacesByHubAsync(new ListSpacesByHubRequest
            {
                HubId = hubId,
                Offset = offset,
                PageSize = pageSize
            }, cancellationToken: ct);
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<SearchSpacesResponse?> SearchSpacesAsync(
        string? query = null, string? hubId = null, string? communityId = null, int limit = 10, CancellationToken ct = default)
    {
        try
        {
            var request = new SearchSpacesRequest { Limit = limit };
            if (!string.IsNullOrEmpty(query)) request.Query = query;
            if (!string.IsNullOrEmpty(hubId)) request.HubId = hubId;
            if (!string.IsNullOrEmpty(communityId)) request.CommunityId = communityId;
            return await spaceClient.SearchSpacesAsync(request, cancellationToken: ct);
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<SpaceInfo?> GetSpaceBySlugAsync(string slug, string hubSlug, CancellationToken ct = default)
    {
        try { return await spaceClient.GetSpaceBySlugAsync(new GetSpaceBySlugRequest { Slug = slug, HubSlug = hubSlug }, cancellationToken: ct); }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<SpaceInfo?> GetSpaceAsync(string publicId, CancellationToken ct = default)
    {
        try { return await spaceClient.GetSpaceAsync(new GetSpaceRequest { PublicId = publicId }, cancellationToken: ct); }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<PagedDiscussionBySpaceList?> GetDiscussionsBySpaceAsync(
        string spaceId, int offset = 0, int pageSize = 20, int? typeFilter = null, string? cursor = null,
        bool viewerAllowsAdult = false, CancellationToken ct = default)
    {
        try
        {
            var request = new GetDiscussionsBySpaceRequest
            {
                SpaceId = spaceId,
                Offset = offset,
                PageSize = pageSize,
                ViewerAllowsAdult = viewerAllowsAdult
            };

            if (typeFilter.HasValue)
                request.TypeFilter = typeFilter.Value;
            if (cursor is not null)
                request.Cursor = cursor;

            return await discussionClient.GetDiscussionsBySpaceAsync(request, cancellationToken: ct);
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<SpaceRulesResponse?> GetSpaceRulesAsync(string spaceId, CancellationToken ct = default)
    {
        try { return await spaceClient.GetSpaceRulesAsync(new GetSpaceRulesRequest { SpaceId = spaceId }, cancellationToken: ct); }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<HubRulesResponse?> GetHubRulesAsync(string hubId, CancellationToken ct = default)
    {
        try { return await hubClient.GetHubRulesAsync(new GetHubRulesRequest { HubId = hubId }, cancellationToken: ct); }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<CommunityRulesResponse?> GetCommunityRulesAsync(string communityId, CancellationToken ct = default)
    {
        try { return await communityClient.GetCommunityRulesAsync(new GetCommunityRulesRequest { CommunityId = communityId }, cancellationToken: ct); }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<SiteRulesResponse?> GetSiteRulesAsync(CancellationToken ct = default)
    {
        try { return await communityClient.GetSiteRulesAsync(new GetSiteRulesRequest(), cancellationToken: ct); }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    // ==================== Discussion ====================

    public virtual async Task<DiscussionInfo?> GetDiscussionAsync(string publicId, CancellationToken ct = default)
    {
        try { return await discussionClient.GetDiscussionAsync(new GetDiscussionRequest { PublicId = publicId }, cancellationToken: ct); }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<IReadOnlyList<DiscussionInfo>> GetDiscussionsByIdsAsync(IEnumerable<string> publicIds, CancellationToken ct = default)
    {
        try
        {
            var req = new GetDiscussionsByIdsRequest();
            req.PublicIds.AddRange(publicIds);
            var result = await discussionClient.GetDiscussionsByIdsAsync(req, cancellationToken: ct);
            return result?.Items ?? (IReadOnlyList<DiscussionInfo>)[];
        }
        catch (RpcException ex) { LogGrpcError(ex); return []; }
    }

    public virtual async Task<PagedRecentDiscussionList?> GetRecentDiscussionsByIdsAsync(IEnumerable<string> publicIds, CancellationToken ct = default)
    {
        try
        {
            var req = new GetRecentDiscussionsByIdsRequest();
            req.PublicIds.AddRange(publicIds);
            return await discussionClient.GetRecentDiscussionsByIdsAsync(req, cancellationToken: ct);
        }
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
        bool pollSecret = false,
        // Link extension
        string? linkUrl = null,
        // Debate extension
        IEnumerable<string>? debatePositions = null,
        bool debateAllowNeutral = false,
        // Images extension
        string? imagesLayout = null,
        IEnumerable<string>? imagesImageUrls = null,
        bool imagesIsSpoiler = false,
        // IAMA extension
        bool iamaIsScheduled = false,
        DateTime? iamaScheduledStart = null,
        DateTime? iamaScheduledEnd = null,
        string? iamaVerificationNote = null,
        // Poll segment extension
        bool pollIsSegmented = false,
        string? pollSegmentLabel = null,
        string? pollSegmentOptionA = null,
        string? pollSegmentOptionB = null,
        bool isAdult = false,
        CancellationToken ct = default)
    {
        try
        {
            var request = new CreateDiscussionRequest { SpaceId = spaceId, Title = title, Content = content, Type = type, IsAdult = isAdult };
            if (tags is not null) request.Tags.AddRange(tags);
            if (pollOptions is not null) request.PollOptions.AddRange(pollOptions);
            request.PollAllowMultiple = pollAllowMultiple;
            request.PollAllowChangeVote = pollAllowChangeVote;
            request.PollSecret = pollSecret;
            if (pollClosesAt.HasValue) request.PollClosesAt = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.SpecifyKind(pollClosesAt.Value, DateTimeKind.Utc));
            if (linkUrl is not null) request.LinkUrl = linkUrl;
            if (debatePositions is not null) request.DebatePositions.AddRange(debatePositions);
            request.DebateAllowNeutral = debateAllowNeutral;
            if (imagesLayout is not null) request.ImagesLayout = imagesLayout;
            if (imagesImageUrls is not null) request.ImagesImageUrls.AddRange(imagesImageUrls);
            request.ImagesIsSpoiler = imagesIsSpoiler;
            request.IamaIsScheduled = iamaIsScheduled;
            if (iamaScheduledStart.HasValue) request.IamaScheduledStart = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.SpecifyKind(iamaScheduledStart.Value, DateTimeKind.Utc));
            if (iamaScheduledEnd.HasValue) request.IamaScheduledEnd = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(DateTime.SpecifyKind(iamaScheduledEnd.Value, DateTimeKind.Utc));
            if (iamaVerificationNote is not null) request.IamaVerificationNote = iamaVerificationNote;
            request.PollIsSegmented = pollIsSegmented;
            if (pollSegmentLabel is not null) request.PollSegmentLabel = pollSegmentLabel;
            if (pollSegmentOptionA is not null) request.PollSegmentOptionA = pollSegmentOptionA;
            if (pollSegmentOptionB is not null) request.PollSegmentOptionB = pollSegmentOptionB;
            return await discussionClient.CreateDiscussionAsync(request, cancellationToken: ct);
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<PagedRecentDiscussionList?> GetRecentDiscussionsAsync(
        int offset = 0, int pageSize = 20, string? communityId = null, string? hubId = null, string? spaceId = null, string? cursor = null, string? authorId = null, IReadOnlyList<string>? spaceIds = null,
        bool viewerAllowsAdult = false, CancellationToken ct = default)
    {
        try
        {
            var request = new GetRecentDiscussionsRequest { Offset = offset, PageSize = pageSize, ViewerAllowsAdult = viewerAllowsAdult };
            if (communityId is not null) request.CommunityId = communityId;
            if (hubId is not null) request.HubId = hubId;
            if (spaceId is not null) request.SpaceId = spaceId;
            if (cursor is not null) request.Cursor = cursor;
            if (authorId is not null) request.AuthorId = authorId;
            if (spaceIds is { Count: > 0 }) request.SpaceIds.AddRange(spaceIds);

            return await discussionClient.GetRecentDiscussionsAsync(request, cancellationToken: ct);
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<PagedRecentDiscussionList?> GetTrendingDiscussionsAsync(
        int offset = 0, int pageSize = 20, string? communityId = null, bool viewerAllowsAdult = false, CancellationToken ct = default)
    {
        try
        {
            var request = new GetTrendingDiscussionsRequest { Offset = offset, PageSize = pageSize, ViewerAllowsAdult = viewerAllowsAdult };
            if (communityId is not null) request.CommunityId = communityId;

            return await discussionClient.GetTrendingDiscussionsAsync(request, cancellationToken: ct);
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<PagedRecentDiscussionList?> GetTopDiscussionsAsync(
        int offset = 0, int pageSize = 20, string? communityId = null, string timePeriod = "week", bool viewerAllowsAdult = false, CancellationToken ct = default)
    {
        try
        {
            var request = new GetTopDiscussionsRequest { Offset = offset, PageSize = pageSize, TimePeriod = timePeriod, ViewerAllowsAdult = viewerAllowsAdult };
            if (communityId is not null) request.CommunityId = communityId;

            return await discussionClient.GetTopDiscussionsAsync(request, cancellationToken: ct);
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<PagedRecentDiscussionList?> GetNewDiscussionsAsync(
        int offset = 0, int pageSize = 20, string? communityId = null, string? cursor = null, bool viewerAllowsAdult = false, CancellationToken ct = default)
    {
        try
        {
            var request = new GetNewDiscussionsRequest { Offset = offset, PageSize = pageSize, ViewerAllowsAdult = viewerAllowsAdult };
            if (communityId is not null) request.CommunityId = communityId;
            if (cursor is not null) request.Cursor = cursor;

            return await discussionClient.GetNewDiscussionsAsync(request, cancellationToken: ct);
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<TopActiveDiscussionsList?> GetTopActiveDiscussionsTodayAsync(
        string? hubId = null,
        string? spaceId = null,
        string? communityId = null,
        bool viewerAllowsAdult = false,
        CancellationToken ct = default)
    {
        try
        {
            var request = new GetTopActiveDiscussionsTodayRequest { Limit = 5, ViewerAllowsAdult = viewerAllowsAdult };
            if (hubId is not null) request.HubId = hubId;
            if (spaceId is not null) request.SpaceId = spaceId;
            if (communityId is not null) request.CommunityId = communityId;

            return await statisticsClient.GetTopActiveDiscussionsTodayAsync(request, cancellationToken: ct);
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<DiscussionPreviewInfo?> GetDiscussionPreviewAsync(string discussionId, CancellationToken ct = default)
    {
        try { return await discussionClient.GetDiscussionPreviewAsync(new GetDiscussionPreviewRequest { DiscussionId = discussionId }, cancellationToken: ct); }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    // ==================== Post ====================

    public virtual async Task<PagedEnrichedPostList?> GetDiscussionPostsAsync(string discussionId, int offset = 0, int pageSize = 20, CancellationToken ct = default)
    {
        try
        {
            return await postClient.GetPostsByDiscussionAsync(new GetPostsByDiscussionRequest
            {
                DiscussionId = discussionId,
                Offset = offset,
                PageSize = pageSize
            }, cancellationToken: ct);
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<string?> CreatePostAsync(string discussionId, string content, string? replyToPostId = null, CancellationToken ct = default)
    {
        try
        {
            var request = new CreatePostRequest { DiscussionId = discussionId, Content = content };
            if (replyToPostId is not null) request.ReplyToPostId = replyToPostId;
            var result = await postClient.CreatePostAsync(request, cancellationToken: ct);

            return result?.PublicId;
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<int> GetPostNumberAsync(string discussionId, string postId, CancellationToken ct = default)
    {
        try
        {
            var result = await postClient.GetPostNumberAsync(new GetPostNumberRequest { DiscussionId = discussionId, PostId = postId }, cancellationToken: ct);
            return result?.PostNumber ?? 1;
        }
        catch (RpcException ex) { LogGrpcError(ex); return 1; }
    }

    public virtual async Task<bool> EditPostAsync(string postId, string userId, string content, CancellationToken ct = default)
    {
        try
        {
            await postClient.EditPostAsync(new EditPostRequest { PostId = postId, Content = content }, cancellationToken: ct);
            return true;
        }
        catch (RpcException ex) { LogGrpcError(ex); return false; }
    }

    // ==================== Read State ====================

    public virtual async Task<ReadStateInfo?> GetReadStateAsync(string userId, string discussionId, CancellationToken ct = default)
    {
        try { return await readStateClient.GetReadStateAsync(new ReadStateGetRequest { DiscussionId = discussionId }, cancellationToken: ct); }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task MarkDiscussionAsReadAsync(string discussionId, string userId, string postId, CancellationToken ct = default)
    {
        try { await readStateClient.MarkAsReadAsync(new ReadStateMarkRequest { DiscussionId = discussionId, LastReadPostId = postId }, cancellationToken: ct); }
        catch (RpcException ex) { LogGrpcError(ex); }
    }

    public virtual async Task BatchUpdateReadStatesAsync(List<(string DiscussionId, string PostId)> updates, CancellationToken ct = default)
    {
        try
        {
            var request = new ReadStateBatchRequest();
            foreach (var (discussionId, postId) in updates)
                request.Items.Add(new ReadStateBatchItem { DiscussionId = discussionId, LastReadPostId = postId });
            await readStateClient.BatchMarkAsReadAsync(request, cancellationToken: ct);
        }
        catch (RpcException ex) { LogGrpcError(ex); }
    }

    // ==================== Top Active / Trending ====================

    public virtual async Task<TopActiveSpacesList?> GetTopActiveSpacesTodayAsync(string? hubId = null, string? communityId = null, CancellationToken ct = default)
    {
        try
        {
            var request = new GetTopActiveSpacesTodayRequest { Limit = 5 };
            if (hubId is not null) request.HubId = hubId;
            if (communityId is not null) request.CommunityId = communityId;

            return await statisticsClient.GetTopActiveSpacesTodayAsync(request, cancellationToken: ct);
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<TopContributorsList?> GetTopContributorsTodayAsync(
        string? hubId = null,
        string? spaceId = null,
        string? communityId = null,
        CancellationToken ct = default)
    {
        try
        {
            var request = new GetTopContributorsTodayRequest { Limit = 5 };
            if (hubId is not null) request.HubId = hubId;
            if (spaceId is not null) request.SpaceId = spaceId;
            if (communityId is not null) request.CommunityId = communityId;

            return await statisticsClient.GetTopContributorsTodayAsync(request, cancellationToken: ct);
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<TopActiveSpacesList?> GetTrendingSpacesAsync(string? hubId = null, string? communityId = null, CancellationToken ct = default)
    {
        try
        {
            var request = new GetTrendingSpacesRequest { Limit = 5 };
            if (hubId is not null) request.HubId = hubId;
            if (communityId is not null) request.CommunityId = communityId;
            return await statisticsClient.GetTrendingSpacesAsync(request, cancellationToken: ct);
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<TopContributorsList?> GetTrendingContributorsAsync(string? hubId = null, string? spaceId = null, string? communityId = null, CancellationToken ct = default)
    {
        try
        {
            var request = new GetTrendingContributorsRequest { Limit = 5 };
            if (hubId is not null) request.HubId = hubId;
            if (spaceId is not null) request.SpaceId = spaceId;
            if (communityId is not null) request.CommunityId = communityId;
            return await statisticsClient.GetTrendingContributorsAsync(request, cancellationToken: ct);
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<TopActiveSpacesList?> GetTopSpacesByPeriodAsync(string period, string? hubId = null, string? communityId = null, CancellationToken ct = default)
    {
        try
        {
            var request = new GetTopSpacesByPeriodRequest { TimePeriod = period, Limit = 5 };
            if (hubId is not null) request.HubId = hubId;
            if (communityId is not null) request.CommunityId = communityId;
            return await statisticsClient.GetTopSpacesByPeriodAsync(request, cancellationToken: ct);
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<TopContributorsList?> GetTopContributorsByPeriodAsync(string period, string? hubId = null, string? spaceId = null, string? communityId = null, CancellationToken ct = default)
    {
        try
        {
            var request = new GetTopContributorsByPeriodRequest { TimePeriod = period, Limit = 5 };
            if (hubId is not null) request.HubId = hubId;
            if (spaceId is not null) request.SpaceId = spaceId;
            if (communityId is not null) request.CommunityId = communityId;
            return await statisticsClient.GetTopContributorsByPeriodAsync(request, cancellationToken: ct);
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<LatestSpacesList?> GetLatestActiveSpacesAsync(string? hubId = null, string? communityId = null, CancellationToken ct = default)
    {
        try
        {
            var request = new GetLatestActiveSpacesRequest { Limit = 5 };
            if (hubId is not null) request.HubId = hubId;
            if (communityId is not null) request.CommunityId = communityId;
            return await statisticsClient.GetLatestActiveSpacesAsync(request, cancellationToken: ct);
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<LatestContributorsList?> GetLatestContributorsAsync(string? hubId = null, string? spaceId = null, string? communityId = null, CancellationToken ct = default)
    {
        try
        {
            var request = new GetLatestContributorsRequest { Limit = 5 };
            if (hubId is not null) request.HubId = hubId;
            if (spaceId is not null) request.SpaceId = spaceId;
            if (communityId is not null) request.CommunityId = communityId;
            return await statisticsClient.GetLatestContributorsAsync(request, cancellationToken: ct);
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    // Backward compatibility aliases
    public virtual Task<TopActiveDiscussionsList?> GetTopActiveDiscussionsAsync(string? communityId = null, bool viewerAllowsAdult = false, CancellationToken ct = default)
        => GetTopActiveDiscussionsTodayAsync(communityId: communityId, viewerAllowsAdult: viewerAllowsAdult, ct: ct);

    public virtual Task<TopActiveSpacesList?> GetTopActiveSpacesAsync(string? communityId = null, CancellationToken ct = default)
        => GetTopActiveSpacesTodayAsync(communityId: communityId, ct: ct);

    public virtual Task<TopContributorsList?> GetTopContributorsAsync(string? communityId = null, CancellationToken ct = default)
        => GetTopContributorsTodayAsync(communityId: communityId, ct: ct);

    // ==================== Stats ====================

    public virtual async Task<PlatformStats?> GetPlatformStatsAsync(CancellationToken ct = default)
    {
        try { return await statisticsClient.GetPlatformStatsAsync(new GetPlatformStatsRequest(), cancellationToken: ct); }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<HubStats?> GetHubStatsAsync(string hubId, CancellationToken ct = default)
    {
        try { return await hubClient.GetHubStatsAsync(new GetHubStatsRequest { PublicId = hubId }, cancellationToken: ct); }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<SpaceStats?> GetSpaceStatsAsync(string spaceId, CancellationToken ct = default)
    {
        try { return await spaceClient.GetSpaceStatsAsync(new GetSpaceStatsRequest { PublicId = spaceId }, cancellationToken: ct); }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<CommunityStats?> GetCommunityStatsAsync(string communityId, CancellationToken ct = default)
    {
        try { return await communityClient.GetCommunityStatsAsync(new GetCommunityStatsRequest { PublicId = communityId }, cancellationToken: ct); }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<DiscussionStats?> GetDiscussionStatsForPopupAsync(string publicId, CancellationToken ct = default)
    {
        try { return await discussionClient.GetDiscussionStatsAsync(new GetDiscussionStatsRequest { PublicId = publicId }, cancellationToken: ct); }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    // ==================== Poll ====================

    public virtual async Task<PollResponse?> GetPollAsync(string discussionId, CancellationToken ct = default)
    {
        try { return await discussionClient.GetPollAsync(new GetPollRequest { DiscussionId = discussionId }, cancellationToken: ct); }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<VotePollResponse?> VotePollAsync(string discussionId, int optionId, int? segmentIndex = null, CancellationToken ct = default)
    {
        try
        {
            var request = new VotePollRequest { DiscussionId = discussionId, OptionId = optionId };
            if (segmentIndex.HasValue) request.SegmentIndex = segmentIndex.Value;
            return await discussionClient.VotePollAsync(request, cancellationToken: ct);
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<VotePollResponse?> RemovePollVoteAsync(string discussionId, int optionId, CancellationToken ct = default)
    {
        try { return await discussionClient.RemovePollVoteAsync(new RemovePollVoteRequest { DiscussionId = discussionId, OptionId = optionId }, cancellationToken: ct); }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    // ==================== Question ====================

    public virtual async Task<QuestionStatusResponse?> GetQuestionStatusAsync(string discussionId, CancellationToken ct = default)
    {
        try { return await discussionClient.GetQuestionStatusAsync(new GetQuestionStatusRequest { DiscussionId = discussionId }, cancellationToken: ct); }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<MarkQuestionSolvedResponse?> MarkQuestionSolvedAsync(string discussionId, string postPublicId, CancellationToken ct = default)
    {
        try { return await discussionClient.MarkQuestionSolvedAsync(new MarkQuestionSolvedRequest { DiscussionId = discussionId, PostPublicId = postPublicId }, cancellationToken: ct); }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    // ==================== Debate ====================

    public virtual async Task<DebateInfoResponse?> GetDebateInfoAsync(string discussionId, CancellationToken ct = default)
    {
        try { return await discussionClient.GetDebateInfoAsync(new GetDebateInfoRequest { DiscussionId = discussionId }, cancellationToken: ct); }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<SetPostDebatePositionResponse?> SetPostDebatePositionAsync(string discussionId, string postPublicId, int positionId, CancellationToken ct = default)
    {
        try { return await discussionClient.SetPostDebatePositionAsync(new SetPostDebatePositionRequest { DiscussionId = discussionId, PostPublicId = postPublicId, PositionId = positionId }, cancellationToken: ct); }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    // ==================== Link ====================

    public virtual async Task<DiscussionLinkResponse?> GetDiscussionLinkAsync(string discussionId, CancellationToken ct = default)
    {
        try { return await discussionClient.GetDiscussionLinkAsync(new GetDiscussionLinkRequest { DiscussionId = discussionId }, cancellationToken: ct); }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    // ==================== Images ====================

    public virtual async Task<ImagesLayoutResponse?> GetImagesDataAsync(string discussionId, CancellationToken ct = default)
    {
        try
        {
            return await discussionClient.GetImagesLayoutAsync(new GetImagesLayoutRequest { DiscussionId = discussionId }, cancellationToken: ct);
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    // ==================== Journal ====================

    public virtual async Task<JournalEntriesResponse?> GetJournalEntriesAsync(string discussionId, CancellationToken ct = default)
    {
        try { return await discussionClient.GetJournalEntriesAsync(new GetJournalEntriesRequest { DiscussionId = discussionId }, cancellationToken: ct); }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<AddJournalEntryResponse?> AddJournalEntryAsync(string discussionId, string postPublicId, CancellationToken ct = default)
    {
        try { return await discussionClient.AddJournalEntryAsync(new AddJournalEntryRequest { DiscussionId = discussionId, PostPublicId = postPublicId }, cancellationToken: ct); }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    // ==================== IAmA ====================

    public virtual async Task<IamaInfoResponse?> GetIamaInfoAsync(string discussionId, CancellationToken ct = default)
    {
        try { return await discussionClient.GetIamaInfoAsync(new GetIamaInfoRequest { DiscussionId = discussionId }, cancellationToken: ct); }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<TransitionIamaPhaseResponse?> TransitionIamaPhaseAsync(string discussionId, int newPhase, CancellationToken ct = default)
    {
        try { return await discussionClient.TransitionIamaPhaseAsync(new TransitionIamaPhaseRequest { DiscussionId = discussionId, NewPhase = newPhase }, cancellationToken: ct); }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<MarkIamaOfficialAnswerResponse?> MarkIamaOfficialAnswerAsync(string discussionId, string questionPostPublicId, string answerPostPublicId, CancellationToken ct = default)
    {
        try { return await discussionClient.MarkIamaOfficialAnswerAsync(new MarkIamaOfficialAnswerRequest { DiscussionId = discussionId, QuestionPostPublicId = questionPostPublicId, AnswerPostPublicId = answerPostPublicId }, cancellationToken: ct); }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    // Entity stats aliases
    public virtual Task<HubStats?> GetHubStatsForPopupAsync(string publicId, CancellationToken ct = default) => GetHubStatsAsync(publicId, ct);
    public virtual Task<SpaceStats?> GetSpaceStatsForPopupAsync(string publicId, CancellationToken ct = default) => GetSpaceStatsAsync(publicId, ct);
    public virtual Task<CommunityStats?> GetCommunityStatsForPopupAsync(string publicId, CancellationToken ct = default) => GetCommunityStatsAsync(publicId, ct);
    public virtual Task<UserStats?> GetUserStatsForPopupAsync(string publicId, CancellationToken ct = default) => GetUserStatsAsync(publicId, ct);

    // ==================== Group Access ====================

    public virtual async Task<CheckGroupAccessResponse?> CheckGroupAccessAsync(
        string communityPublicId,
        string? hubPublicId = null,
        string? spacePublicId = null,
        CancellationToken ct = default)
    {
        try
        {
            var request = new CheckGroupAccessRequest { CommunityPublicId = communityPublicId };
            if (hubPublicId is not null) request.HubPublicId = hubPublicId;
            if (spacePublicId is not null) request.SpacePublicId = spacePublicId;
            return await communityClient.CheckGroupAccessAsync(request, cancellationToken: ct);
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    // ==================== Search ====================

    public virtual async Task<PagedDiscussionSearchResults?> SearchDiscussionsAsync(
        string? query = null, string? authorPublicId = null, string? spacePublicId = null,
        string? hubPublicId = null, int offset = 0, int pageSize = 20, bool viewerAllowsAdult = false,
        string? sortBy = null, string? dateRange = null, CancellationToken ct = default)
    {
        var request = new SearchDiscussionsRequest { Query = query ?? "", Offset = offset, PageSize = pageSize, ViewerAllowsAdult = viewerAllowsAdult };
        if (authorPublicId is not null) request.AuthorId = authorPublicId;
        if (spacePublicId is not null) request.SpaceId = spacePublicId;
        if (hubPublicId is not null) request.HubId = hubPublicId;
        if (!string.IsNullOrEmpty(sortBy)) request.SortBy = sortBy;
        if (!string.IsNullOrEmpty(dateRange)) request.DateRange = dateRange;

        // Let exceptions propagate so callers can distinguish API failure from empty results.
        return await searchClient.SearchDiscussionsAsync(request, cancellationToken: ct);
    }

    public virtual async Task<PagedPostSearchResults?> SearchPostsAsync(
        string? query = null, string? authorPublicId = null, string? discussionPublicId = null,
        string? spacePublicId = null, int offset = 0, int pageSize = 20,
        string? sortBy = null, string? dateRange = null, CancellationToken ct = default)
    {
        var request = new SearchPostsRequest { Query = query ?? "", Offset = offset, PageSize = pageSize };
        if (authorPublicId is not null) request.AuthorId = authorPublicId;
        if (discussionPublicId is not null) request.DiscussionId = discussionPublicId;
        if (spacePublicId is not null) request.SpaceId = spacePublicId;
        if (!string.IsNullOrEmpty(sortBy)) request.SortBy = sortBy;
        if (!string.IsNullOrEmpty(dateRange)) request.DateRange = dateRange;

        // Let exceptions propagate so callers can distinguish API failure from empty results.
        return await searchClient.SearchPostsAsync(request, cancellationToken: ct);
    }

    public virtual async Task<UserProfileInfo?> GetUserProfileAsync(string publicId, CancellationToken ct = default)
    {
        try { return await userClient.GetUserProfileAsync(new GetUserProfileRequest { PublicId = publicId }, cancellationToken: ct); }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    // ==================== Auth ====================

    public virtual async Task<AuthStatusResponse?> GetAuthStatusAsync(CancellationToken ct = default)
    {
        try { return await authClient.GetAuthStatusAsync(new GetAuthStatusRequest(), cancellationToken: ct); }
        catch (RpcException ex) { LogGrpcError(ex); return new AuthStatusResponse { IsAuthenticated = false }; }
    }

    public virtual async Task<CurrentUserResponse?> GetCurrentUserAsync(CancellationToken ct = default)
    {
        try
        {
            var result = await authClient.GetCurrentUserAsync(new GetCurrentUserRequest(), cancellationToken: ct);
            return result.IsAuthenticated ? result : null;
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<DisplayNameHistoryResponse?> GetDisplayNameHistoryAsync(CancellationToken ct = default)
    {
        try { return await authClient.GetDisplayNameHistoryAsync(new GetDisplayNameHistoryRequest(), cancellationToken: ct); }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<UpdateProfileResponse?> UpdateProfileAsync(string displayName, string? password = null, string? turnstileToken = null, string? sudoToken = null, CancellationToken ct = default)
    {
        try
        {
            var request = new UpdateProfileRequest { DisplayName = displayName };
            if (password is not null) request.Password = password;
            if (turnstileToken is not null) request.TurnstileToken = turnstileToken;
            if (sudoToken is not null) request.SudoToken = sudoToken;
            return await authClient.UpdateProfileAsync(request, cancellationToken: ct);
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<bool> UpdatePreferencesAsync(bool? autoFollowOnReply = null, string? timezone = null, string? bio = null, bool? allowAdultContent = null, bool clearAllowAdultContent = false, int? adultPreviewImageMode = null, bool? hidePresence = null, CancellationToken ct = default)
    {
        try
        {
            var request = new UpdatePreferencesRequest();
            if (autoFollowOnReply.HasValue) request.AutoFollowOnReply = autoFollowOnReply.Value;
            if (timezone is not null) request.Timezone = timezone;
            if (bio is not null) request.Bio = bio;
            if (allowAdultContent.HasValue) request.AllowAdultContent = allowAdultContent.Value;
            request.ResetAdultContentToAsk = clearAllowAdultContent;
            if (adultPreviewImageMode.HasValue) request.AdultPreviewImageMode = adultPreviewImageMode.Value;
            if (hidePresence.HasValue) request.HidePresence = hidePresence.Value;
            await authClient.UpdatePreferencesAsync(request, cancellationToken: ct);

            return true;
        }
        catch (RpcException ex) { LogGrpcError(ex); return false; }
    }

    public virtual async Task<string?> GenerateFeedTokenAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await authClient.GenerateFeedTokenAsync(new GenerateFeedTokenRequest(), cancellationToken: ct);
            return response.Token;
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<bool> RevokeFeedTokenAsync(CancellationToken ct = default)
    {
        try
        {
            await authClient.RevokeFeedTokenAsync(new RevokeFeedTokenRequest(), cancellationToken: ct);
            return true;
        }
        catch (RpcException ex) { LogGrpcError(ex); return false; }
    }

    public virtual async Task<GenerateDiscordLinkTokenResponse?> GenerateDiscordLinkTokenAsync(CancellationToken ct = default)
    {
        try { return await authClient.GenerateDiscordLinkTokenAsync(new GenerateDiscordLinkTokenRequest(), cancellationToken: ct); }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<bool> UnlinkDiscordAsync(CancellationToken ct = default)
    {
        try
        {
            await authClient.UnlinkDiscordAsync(new UnlinkDiscordRequest(), cancellationToken: ct);
            return true;
        }
        catch (RpcException ex) { LogGrpcError(ex); return false; }
    }

    public virtual async Task<DiscordStatusResponse?> GetDiscordStatusAsync(CancellationToken ct = default)
    {
        try { return await authClient.GetDiscordStatusAsync(new GetDiscordStatusRequest(), cancellationToken: ct); }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task LogoutAsync(CancellationToken ct = default)
    {
        try { await authClient.LogoutAsync(new LogoutRequest(), cancellationToken: ct); }
        catch (RpcException ex) { LogGrpcError(ex); }
    }

    public virtual async Task<string?> GetSiteTimezoneAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await authClient.GetPublicSettingsAsync(new GetPublicSettingsRequest(), cancellationToken: ct);
            return string.IsNullOrEmpty(response.Timezone) ? null : response.Timezone;
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<bool> IsHealthyAsync(CancellationToken ct = default)
    {
        try
        {
            await statisticsClient.GetPlatformStatsAsync(new GetPlatformStatsRequest(), cancellationToken: ct);
            return true;
        }
        catch (Exception ex) { LogGrpcError(ex); return false; }
    }

    // ==================== Follow ====================

    // Space follow
    public virtual async Task<SpaceFollowStatusResponse?> GetSpaceFollowStatusAsync(string spaceId, CancellationToken ct = default)
    {
        try { return await followClient.GetSpaceFollowStatusAsync(new GetSpaceFollowStatusRequest { SpaceId = spaceId }, cancellationToken: ct); }
        catch (RpcException ex) { LogGrpcError(ex); return new SpaceFollowStatusResponse { IsFollowing = false }; }
    }

    public virtual async Task<SpaceFollowToggleResponse?> ToggleSpaceFollowAsync(string spaceId, string? level, CancellationToken ct = default)
    {
        try
        {
            var request = new ToggleSpaceFollowRequest { SpaceId = spaceId };
            if (level is not null) request.Level = level;

            return await followClient.ToggleSpaceFollowAsync(request, cancellationToken: ct);
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<FollowLevelResponse?> SetSpaceFollowLevelAsync(string spaceId, string level, CancellationToken ct = default)
    {
        try { return await followClient.SetSpaceFollowLevelAsync(new SetSpaceFollowLevelRequest { SpaceId = spaceId, Level = level }, cancellationToken: ct); }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    // Discussion follow
    public virtual async Task<FollowToggleResponse?> GetDiscussionFollowStatusAsync(string discussionId, CancellationToken ct = default)
    {
        try { return await followClient.GetDiscussionFollowStatusAsync(new GetDiscussionFollowStatusRequest { DiscussionId = discussionId }, cancellationToken: ct); }
        catch (RpcException ex) { LogGrpcError(ex); return new FollowToggleResponse { IsFollowing = false }; }
    }

    public virtual async Task<FollowToggleResponse?> ToggleDiscussionFollowAsync(string discussionId, CancellationToken ct = default)
    {
        try { return await followClient.ToggleDiscussionFollowAsync(new ToggleDiscussionFollowRequest { DiscussionId = discussionId }, cancellationToken: ct); }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    // User follow
    public virtual async Task<FollowToggleResponse?> GetUserFollowStatusAsync(string userId, string currentUserId, CancellationToken ct = default)
    {
        try { return await followClient.GetUserFollowStatusAsync(new GetUserFollowStatusRequest { UserId = userId }, cancellationToken: ct); }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<FollowToggleResponse?> ToggleUserFollowAsync(string userId, CancellationToken ct = default)
    {
        try { return await followClient.ToggleUserFollowAsync(new ToggleUserFollowRequest { UserId = userId }, cancellationToken: ct); }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    // Follow lists
    public virtual async Task<List<string>> GetFollowedSpacesAsync(CancellationToken ct = default)
    {
        try
        {
            var result = await followClient.GetFollowedSpacesAsync(new GetFollowedSpacesRequest(), cancellationToken: ct);
            return result?.PublicIds?.ToList() ?? [];
        }
        catch (RpcException ex) { LogGrpcError(ex); return []; }
    }

    public virtual async Task<List<string>> GetFollowedDiscussionsAsync(CancellationToken ct = default)
    {
        try
        {
            var result = await followClient.GetFollowedDiscussionsAsync(new GetFollowedDiscussionsRequest(), cancellationToken: ct);
            return result?.PublicIds?.ToList() ?? [];
        }
        catch (RpcException ex) { LogGrpcError(ex); return []; }
    }

    public virtual async Task<List<string>> GetFollowedUsersAsync(CancellationToken ct = default)
    {
        try
        {
            var result = await followClient.GetFollowedUsersAsync(new GetFollowedUsersRequest(), cancellationToken: ct);
            return result?.PublicIds?.ToList() ?? [];
        }
        catch (RpcException ex) { LogGrpcError(ex); return []; }
    }

    // ==================== Reactions ====================

    public virtual async Task<Dictionary<string, int>?> GetPostReactionsAsync(string postId, CancellationToken ct = default)
    {
        try
        {
            var result = await reactionClient.GetReactionCountsAsync(new GetReactionCountsRequest { PostId = postId }, cancellationToken: ct);
            return result?.Counts?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value) ?? new Dictionary<string, int>();
        }
        catch (RpcException ex) { LogGrpcError(ex); return new Dictionary<string, int>(); }
    }

    public virtual async Task<List<string>?> GetMyPostReactionsAsync(string postId, CancellationToken ct = default)
    {
        try
        {
            var result = await reactionClient.GetMyReactionsAsync(new GetMyReactionsRequest { PostId = postId }, cancellationToken: ct);
            return result?.Reactions?.ToList() ?? [];
        }
        catch (RpcException ex) { LogGrpcError(ex); return []; }
    }

    public virtual async Task TogglePostReactionAsync(string postId, int type, CancellationToken ct = default)
    {
        try { await reactionClient.ToggleReactionAsync(new ToggleReactionRequest { PostId = postId, ReactionType = type.ToString() }, cancellationToken: ct); }
        catch (RpcException ex) { LogGrpcError(ex); }
    }

    public virtual async Task<PagedReactedPostsList?> GetMyReactedPostsAsync(int offset = 0, int pageSize = 20, CancellationToken ct = default)
    {
        try { return await reactionClient.GetMyReactedPostsAsync(new GetMyReactedPostsRequest { Offset = offset, PageSize = pageSize }, cancellationToken: ct); }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<PagedReactedPostsList?> GetMyReactedDiscussionsAsync(int offset = 0, int pageSize = 20, CancellationToken ct = default)
    {
        try { return await reactionClient.GetMyReactedDiscussionsAsync(new GetMyReactedPostsRequest { Offset = offset, PageSize = pageSize }, cancellationToken: ct); }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    // ==================== Notifications ====================

    public virtual async Task<PagedNotificationList?> GetNotificationsAsync(int offset = 0, int pageSize = 10, CancellationToken ct = default)
    {
        try { return await notificationClient.GetNotificationsAsync(new NotifGetRequest { Offset = offset, PageSize = pageSize }, cancellationToken: ct); }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<UnreadCountResponse?> GetUnreadNotificationCountAsync(CancellationToken ct = default)
    {
        try { return await notificationClient.GetUnreadCountAsync(new NotifUnreadRequest(), cancellationToken: ct); }
        catch (RpcException ex) { LogGrpcError(ex); return new UnreadCountResponse { Count = 0 }; }
    }

    public virtual async Task MarkNotificationAsReadAsync(string notificationId, CancellationToken ct = default)
    {
        try { await notificationClient.MarkAsReadAsync(new NotifMarkReadRequest { NotificationId = notificationId }, cancellationToken: ct); }
        catch (RpcException ex) { LogGrpcError(ex); }
    }

    public virtual async Task MarkAllNotificationsAsReadAsync(CancellationToken ct = default)
    {
        try { await notificationClient.MarkAllAsReadAsync(new NotifMarkAllReadRequest(), cancellationToken: ct); }
        catch (RpcException ex) { LogGrpcError(ex); }
    }

    // ==================== Markup ====================

    public virtual async Task<string?> PreviewMarkupAsync(string content, CancellationToken ct = default)
    {
        try
        {
            var result = await markupClient.PreviewAsync(new PreviewMarkupRequest { Content = content }, cancellationToken: ct);
            return result?.Html;
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    // ==================== User ====================

    public virtual async Task<UserStats?> GetUserStatsAsync(string userId, CancellationToken ct = default)
    {
        try { return await statisticsClient.GetUserStatsAsync(new GetUserStatsRequest { PublicId = userId }, cancellationToken: ct); }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<UserActivityHistory?> GetUserActivityHistoryAsync(string userId, int days = 30, CancellationToken ct = default)
    {
        try { return await statisticsClient.GetUserActivityHistoryAsync(new GetUserActivityHistoryRequest { PublicId = userId, Days = days }, cancellationToken: ct); }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<SparklineResponse?> GetActivitySparklineAsync(string entityType, string? publicId, int days = 7, CancellationToken ct = default)
    {
        try
        {
            return await statisticsClient.GetActivitySparklineAsync(new SparklineRequest
            {
                EntityType = entityType,
                PublicId   = publicId ?? "",
                Days       = days
            }, cancellationToken: ct);
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<SparklineBatchResponse?> GetActivitySparklinesBatchAsync(IEnumerable<string> publicIds, int days = 7, CancellationToken ct = default)
    {
        try
        {
            var request = new SparklineBatchRequest { Days = days };
            request.PublicIds.AddRange(publicIds);
            return await statisticsClient.GetActivitySparklinesBatchAsync(request, cancellationToken: ct);
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task RecordBatchViewsAsync(IReadOnlyDictionary<(string DiscussionPublicId, string CountryCode), long> counts, CancellationToken ct = default)
    {
        if (counts.Count == 0) return;
        try
        {
            var request = new Snakk.Protos.Statistics.RecordBatchViewsRequest();
            foreach (var ((publicId, country), count) in counts)
                request.Entries.Add(new Snakk.Protos.Statistics.DiscussionViewEntry
                {
                    DiscussionPublicId = publicId,
                    CountryCode = country,
                    Count = count
                });
            await statisticsClient.RecordBatchViewsAsync(request, cancellationToken: ct);
        }
        catch (RpcException ex) { LogGrpcError(ex); }
    }

    // Endless scroll (alias for GetDiscussionsBySpaceAsync)
    public virtual Task<PagedDiscussionBySpaceList?> GetSpaceDiscussionsAsync(string spaceId, int offset, int pageSize, string? cursor = null, bool viewerAllowsAdult = false, CancellationToken ct = default)
        => GetDiscussionsBySpaceAsync(spaceId, offset, pageSize, cursor: cursor, viewerAllowsAdult: viewerAllowsAdult, ct: ct);

    // ==================== Moderation ====================

    public virtual async Task<bool> CanModerateAsync(string? communityId = null, string? hubId = null, string? spaceId = null, CancellationToken ct = default)
    {
        try
        {
            var request = new CanModerateRequest();
            if (communityId is not null) request.CommunityId = communityId;
            if (hubId is not null) request.HubId = hubId;
            if (spaceId is not null) request.SpaceId = spaceId;
            var result = await moderationClient.CanModerateAsync(request, cancellationToken: ct);

            return result.CanModerate;
        }
        catch (RpcException ex) { LogGrpcError(ex); return false; }
    }

    public virtual async Task<bool> CanAdministerAsync(string? communityId = null, string? hubId = null, string? spaceId = null, CancellationToken ct = default)
    {
        try
        {
            var request = new CanAdministerRequest();
            if (communityId is not null) request.CommunityId = communityId;
            if (hubId is not null) request.HubId = hubId;
            if (spaceId is not null) request.SpaceId = spaceId;
            var result = await moderationClient.CanAdministerAsync(request, cancellationToken: ct);

            return result.CanAdminister;
        }
        catch (RpcException ex) { LogGrpcError(ex); return false; }
    }

    // Role management — returns local DTOs mapped from proto
    public virtual async Task<IEnumerable<UserRoleDto>?> GetMyRolesAsync(CancellationToken ct = default)
    {
        try
        {
            var result = await moderationClient.GetMyRolesAsync(new GetMyRolesRequest(), cancellationToken: ct);
            return result.Items.Select(MapRoleInfo);
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<IEnumerable<UserRoleDto>?> GetUserRolesAsync(string userId, CancellationToken ct = default)
    {
        try
        {
            var result = await moderationClient.GetUserRolesAsync(new GetUserRolesRequest { UserId = userId }, cancellationToken: ct);
            return result.Items.Select(MapRoleInfo);
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<IEnumerable<UserRoleDto>?> GetRolesForCommunityAsync(string communityId, CancellationToken ct = default)
    {
        try
        {
            var result = await moderationClient.GetRolesForCommunityAsync(new GetRolesForScopeRequest { ScopeId = communityId }, cancellationToken: ct);
            return result.Items.Select(MapRoleInfo);
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<IEnumerable<UserRoleDto>?> GetRolesForHubAsync(string hubId, CancellationToken ct = default)
    {
        try
        {
            var result = await moderationClient.GetRolesForHubAsync(new GetRolesForScopeRequest { ScopeId = hubId }, cancellationToken: ct);
            return result.Items.Select(MapRoleInfo);
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<IEnumerable<UserRoleDto>?> GetRolesForSpaceAsync(string spaceId, CancellationToken ct = default)
    {
        try
        {
            var result = await moderationClient.GetRolesForSpaceAsync(new GetRolesForScopeRequest { ScopeId = spaceId }, cancellationToken: ct);
            return result.Items.Select(MapRoleInfo);
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<UserRoleDto?> AssignRoleAsync(Snakk.Web.Models.AssignRoleRequest request, CancellationToken ct = default)
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
            var result = await moderationClient.AssignRoleAsync(grpcRequest, cancellationToken: ct);

            return MapRoleInfo(result);
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<bool> RevokeRoleAsync(string roleId, CancellationToken ct = default)
    {
        try
        {
            var result = await moderationClient.RevokeRoleAsync(new RevokeRoleRequest { RoleId = roleId }, cancellationToken: ct);
            return result.Success;
        }
        catch (RpcException ex) { LogGrpcError(ex); return false; }
    }

    // Public moderator list
    public virtual async Task<GetModeratorsResponse?> GetModeratorsAsync(string scopeType, string scopePublicId, CancellationToken ct = default)
    {
        try
        {
            return await moderationClient.GetModeratorsAsync(
                new GetModeratorsRequest { ScopeType = scopeType, ScopePublicId = scopePublicId }, cancellationToken: ct);
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual Task<GetModeratorsResponse?> GetSiteModeratorsAsync(CancellationToken ct = default)
        => GetModeratorsAsync("Platform", "", ct);

    // Ban management
    public virtual async Task<IEnumerable<UserBanDto>?> GetUserBansAsync(string userId, CancellationToken ct = default)
    {
        try
        {
            var result = await moderationClient.GetUserBansAsync(new GetUserBansRequest { UserId = userId }, cancellationToken: ct);
            return result.Items.Select(MapBanInfo);
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<BanCheckResult?> CheckUserBanAsync(
        string userId,
        string? communityId = null,
        string? hubId = null,
        string? spaceId = null,
        CancellationToken ct = default)
    {
        try
        {
            var request = new CheckUserBanRequest { UserId = userId };
            if (communityId is not null) request.CommunityId = communityId;
            if (hubId is not null) request.HubId = hubId;
            if (spaceId is not null) request.SpaceId = spaceId;
            var result = await moderationClient.CheckUserBanAsync(request, cancellationToken: ct);

            return new BanCheckResult(result.IsBanned, result.Ban is not null ? MapBanInfo(result.Ban) : null);
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<UserBanDto?> BanUserAsync(Snakk.Web.Models.BanUserRequest request, CancellationToken ct = default)
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
            var result = await moderationClient.BanUserAsync(grpcRequest, cancellationToken: ct);

            return MapBanInfo(result);
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<bool> UnbanUserAsync(string banId, CancellationToken ct = default)
    {
        try
        {
            var result = await moderationClient.UnbanUserAsync(new UnbanUserRequest { BanId = banId }, cancellationToken: ct);
            return result.Success;
        }
        catch (RpcException ex) { LogGrpcError(ex); return false; }
    }

    // Report management
    public virtual async Task<int> GetPendingReportCountAsync(CancellationToken ct = default)
    {
        try
        {
            var result = await moderationClient.GetPendingReportCountAsync(new GetPendingReportCountRequest(), cancellationToken: ct);
            return result.Count;
        }
        catch (RpcException ex) { LogGrpcError(ex); return 0; }
    }

    public virtual async Task<PagedResult<ReportListDto>?> GetReportsAsync(string? status = null, int offset = 0, int pageSize = 20, CancellationToken ct = default)
    {
        try
        {
            var request = new GetReportsRequest { Offset = offset, PageSize = pageSize };
            if (status is not null && int.TryParse(status, out var statusId)) request.StatusId = statusId;
            var result = await moderationClient.GetReportsAsync(request, cancellationToken: ct);

            return new PagedResult<ReportListDto>(
                result.Items.Select(MapReportListItem),
                result.Offset,
                result.PageSize,
                result.Total > result.Offset + result.PageSize);
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<ReportDetailDto?> GetReportDetailAsync(string reportId, CancellationToken ct = default)
    {
        try
        {
            var r = await moderationClient.GetReportDetailAsync(new GetReportDetailRequest { ReportId = reportId }, cancellationToken: ct);
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

    public virtual async Task<ReportDto?> CreateReportAsync(Snakk.Web.Models.CreateReportRequest request, CancellationToken ct = default)
    {
        try
        {
            var grpcRequest = new Snakk.Protos.Moderation.CreateReportRequest();
            if (request.ReportedPostId is not null) grpcRequest.PostId = request.ReportedPostId;
            if (request.ReportedDiscussionId is not null) grpcRequest.DiscussionId = request.ReportedDiscussionId;
            if (request.ReportedUserId is not null) grpcRequest.UserId = request.ReportedUserId;
            if (request.ReasonId is not null) grpcRequest.ReasonId = request.ReasonId;
            if (request.Details is not null) grpcRequest.Details = request.Details;
            var result = await moderationClient.CreateReportAsync(grpcRequest, cancellationToken: ct);

            return new ReportDto(result.PublicId, result.Status, "", null, null, null, null, null,
                result.CreatedAt.ToDateTime(), null, null, null);
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<bool> ResolveReportAsync(string reportId, Snakk.Web.Models.ResolveReportRequest request, CancellationToken ct = default)
    {
        try
        {
            var grpcRequest = new Snakk.Protos.Moderation.ResolveReportRequest
            {
                ReportId = reportId,
                Dismiss = request.Dismiss
            };
            if (request.ResolutionNote is not null) grpcRequest.ResolutionNote = request.ResolutionNote;
            var result = await moderationClient.ResolveReportAsync(grpcRequest, cancellationToken: ct);

            return result.Success;
        }
        catch (RpcException ex) { LogGrpcError(ex); return false; }
    }

    public virtual async Task<ReportCommentDto?> AddReportCommentAsync(string reportId, Snakk.Web.Models.AddReportCommentRequest request, CancellationToken ct = default)
    {
        try
        {
            var result = await moderationClient.AddReportCommentAsync(
                new Snakk.Protos.Moderation.AddReportCommentRequest { ReportId = reportId, Content = request.Content }, cancellationToken: ct);
            return new ReportCommentDto(
                result.PublicId, result.AuthorUserPublicId, result.AuthorUserDisplayName,
                result.Content, result.CreatedAt.ToDateTime(), result.EditedAt?.ToDateTime());
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<IEnumerable<ReportReasonDto>?> GetReportReasonsAsync(
        string? communityId = null,
        string? hubId = null,
        string? spaceId = null,
        CancellationToken ct = default)
    {
        try
        {
            var request = new GetReportReasonsRequest();
            if (spaceId is not null) request.SpaceId = spaceId;
            var result = await moderationClient.GetReportReasonsAsync(request, cancellationToken: ct);

            return result.Items.Select(r => new ReportReasonDto(
                r.PublicId, r.Name, r.Description,
                r.CommunityPublicId, r.HubPublicId, r.SpacePublicId, r.DisplayOrder));
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    // Moderation log
    public virtual async Task<PagedResult<ModerationLogDto>?> GetModerationLogsAsync(
        string? communityId = null, string? hubId = null, string? spaceId = null,
        int offset = 0, int pageSize = 20, CancellationToken ct = default)
    {
        try
        {
            var request = new GetModerationLogsRequest { Offset = offset, PageSize = pageSize };
            if (communityId is not null) request.CommunityId = communityId;
            if (hubId is not null) request.HubId = hubId;
            if (spaceId is not null) request.SpaceId = spaceId;
            var result = await moderationClient.GetModerationLogsAsync(request, cancellationToken: ct);

            return new PagedResult<ModerationLogDto>(
                result.Items.Select(MapModerationLogItem),
                result.Offset,
                result.PageSize,
                result.Total > result.Offset + result.PageSize);
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    // Content moderation
    public virtual async Task<bool> DeletePostAsync(string postId, string? reason = null, CancellationToken ct = default)
    {
        try
        {
            var request = new DeletePostRequest { PostId = postId };
            if (reason is not null) request.Reason = reason;
            var result = await moderationClient.DeletePostAsync(request, cancellationToken: ct);

            return result.Success;
        }
        catch (RpcException ex) { LogGrpcError(ex); return false; }
    }

    public virtual async Task<bool> DeleteDiscussionAsync(string discussionId, string? reason = null, CancellationToken ct = default)
    {
        try
        {
            var request = new DeleteDiscussionRequest { DiscussionId = discussionId };
            if (reason is not null) request.Reason = reason;
            var result = await moderationClient.DeleteDiscussionAsync(request, cancellationToken: ct);

            return result.Success;
        }
        catch (RpcException ex) { LogGrpcError(ex); return false; }
    }

    public virtual async Task<bool> LockDiscussionAsync(string discussionId, string? reason = null, CancellationToken ct = default)
    {
        try
        {
            var request = new LockDiscussionRequest { DiscussionId = discussionId };
            if (reason is not null) request.Reason = reason;
            var result = await moderationClient.LockDiscussionAsync(request, cancellationToken: ct);

            return result.Success;
        }
        catch (RpcException ex) { LogGrpcError(ex); return false; }
    }

    public virtual async Task<bool> UnlockDiscussionAsync(string discussionId, CancellationToken ct = default)
    {
        try
        {
            var result = await moderationClient.UnlockDiscussionAsync(new UnlockDiscussionRequest { DiscussionId = discussionId }, cancellationToken: ct);
            return result.Success;
        }
        catch (RpcException ex) { LogGrpcError(ex); return false; }
    }

    // ==================== GrpcResult<T> Overloads ====================
    // These return typed results with error differentiation.
    // Callers can distinguish NotFound vs Unauthenticated vs ServerError.

    public virtual Task<GrpcResult<CommunityInfo>> GetCommunityBySlugResultAsync(string slug, CancellationToken ct = default) =>
        CallAsync(() => communityClient.GetCommunityBySlugAsync(
            new GetCommunityBySlugRequest { Slug = slug }, cancellationToken: ct).ResponseAsync);

    public virtual Task<GrpcResult<HubInfo>> GetHubBySlugResultAsync(string slug, string communitySlug, CancellationToken ct = default) =>
        CallAsync(() => hubClient.GetHubBySlugAsync(
            new GetHubBySlugRequest { Slug = slug, CommunitySlug = communitySlug }, cancellationToken: ct).ResponseAsync);

    public virtual Task<GrpcResult<SpaceInfo>> GetSpaceBySlugResultAsync(string slug, string hubSlug, CancellationToken ct = default) =>
        CallAsync(() => spaceClient.GetSpaceBySlugAsync(
            new GetSpaceBySlugRequest { Slug = slug, HubSlug = hubSlug }, cancellationToken: ct).ResponseAsync);

    public virtual Task<GrpcResult<DiscussionInfo>> GetDiscussionResultAsync(string publicId, CancellationToken ct = default) =>
        CallAsync(() => discussionClient.GetDiscussionAsync(
            new GetDiscussionRequest { PublicId = publicId }, cancellationToken: ct).ResponseAsync);

    public virtual Task<GrpcResult<UserProfileInfo>> GetUserProfileResultAsync(string publicId, CancellationToken ct = default) =>
        CallAsync(() => userClient.GetUserProfileAsync(
            new GetUserProfileRequest { PublicId = publicId }, cancellationToken: ct).ResponseAsync);

    public virtual Task<GrpcResult<SpaceFollowToggleResponse>> ToggleSpaceFollowResultAsync(string spaceId, string? level, CancellationToken ct = default)
    {
        var request = new ToggleSpaceFollowRequest { SpaceId = spaceId };
        if (level is not null) request.Level = level;
        return CallAsync(() => followClient.ToggleSpaceFollowAsync(request, cancellationToken: ct).ResponseAsync);
    }

    public virtual Task<GrpcResult<FollowToggleResponse>> ToggleDiscussionFollowResultAsync(string discussionId, CancellationToken ct = default) =>
        CallAsync(() => followClient.ToggleDiscussionFollowAsync(
            new ToggleDiscussionFollowRequest { DiscussionId = discussionId }, cancellationToken: ct).ResponseAsync);

    public virtual Task<GrpcResult<FollowToggleResponse>> ToggleUserFollowResultAsync(string userId, CancellationToken ct = default) =>
        CallAsync(() => followClient.ToggleUserFollowAsync(
            new ToggleUserFollowRequest { UserId = userId }, cancellationToken: ct).ResponseAsync);

    public virtual Task<GrpcResult<PostCreatedInfo>> CreatePostResultAsync(string discussionId, string content, string? replyToPostId = null, CancellationToken ct = default)
    {
        var request = new CreatePostRequest { DiscussionId = discussionId, Content = content };
        if (replyToPostId is not null) request.ReplyToPostId = replyToPostId;
        return CallAsync(() => postClient.CreatePostAsync(request, cancellationToken: ct).ResponseAsync);
    }

    public virtual Task<GrpcResult<EditPostResponse>> EditPostResultAsync(string postId, string content, CancellationToken ct = default) =>
        CallAsync(() => postClient.EditPostAsync(
            new EditPostRequest { PostId = postId, Content = content }, cancellationToken: ct).ResponseAsync);

    public virtual Task<GrpcResult<EditDiscussionResponse>> EditDiscussionTitleResultAsync(string discussionId, string newTitle, CancellationToken ct = default) =>
        CallAsync(() => discussionClient.EditDiscussionAsync(
            new EditDiscussionRequest { DiscussionId = discussionId, NewTitle = newTitle }, cancellationToken: ct).ResponseAsync);

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

    // ==================== Banners ====================

    public async Task<Snakk.Protos.Banner.BannerList?> GetActiveBannersForCommunityAsync(string communityPublicId, CancellationToken ct = default)
    {
        try
        {
            return await bannerClient.GetActiveForCommunityAsync(
                new Snakk.Protos.Banner.GetActiveBannersRequest { EntityId = communityPublicId }, cancellationToken: ct);
        }
        catch (RpcException ex)
        {
            LogGrpcError(ex);
            return null;
        }
    }

    public async Task<Snakk.Protos.Banner.BannerList?> GetActiveBannersForHubAsync(string hubPublicId, CancellationToken ct = default)
    {
        try
        {
            return await bannerClient.GetActiveForHubAsync(
                new Snakk.Protos.Banner.GetActiveBannersRequest { EntityId = hubPublicId }, cancellationToken: ct);
        }
        catch (RpcException ex)
        {
            LogGrpcError(ex);
            return null;
        }
    }

    public async Task<Snakk.Protos.Banner.BannerList?> GetActiveBannersForSpaceAsync(string spacePublicId, CancellationToken ct = default)
    {
        try
        {
            return await bannerClient.GetActiveForSpaceAsync(
                new Snakk.Protos.Banner.GetActiveBannersRequest { EntityId = spacePublicId }, cancellationToken: ct);
        }
        catch (RpcException ex)
        {
            LogGrpcError(ex);
            return null;
        }
    }

    // ==================== Consent ====================

    public virtual async Task<Snakk.Protos.Consent.GetConsentTextResponse?> GetConsentTextAsync(string slug, CancellationToken ct = default)
    {
        try
        {
            return await consentClient.GetConsentTextAsync(
                new Snakk.Protos.Consent.GetConsentTextRequest { Slug = slug }, cancellationToken: ct);
        }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    // ==================== Saves ====================

    public virtual async Task<SaveToggleResponse?> ToggleSaveDiscussionAsync(string discussionId, CancellationToken ct = default)
    {
        try { return await saveClient.ToggleSaveDiscussionAsync(new ToggleSaveDiscussionRequest { DiscussionId = discussionId }, cancellationToken: ct); }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<SaveToggleResponse?> ToggleSavePostAsync(string postId, CancellationToken ct = default)
    {
        try { return await saveClient.ToggleSavePostAsync(new ToggleSavePostRequest { PostId = postId }, cancellationToken: ct); }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<List<string>> GetSavedDiscussionIdsAsync(CancellationToken ct = default)
    {
        try
        {
            var result = await saveClient.GetSavedDiscussionIdsAsync(new GetSavedIdsRequest(), cancellationToken: ct);
            return result?.PublicIds?.ToList() ?? [];
        }
        catch (RpcException ex) { LogGrpcError(ex); return []; }
    }

    public virtual async Task<List<string>> GetSavedPostIdsAsync(CancellationToken ct = default)
    {
        try
        {
            var result = await saveClient.GetSavedPostIdsAsync(new GetSavedIdsRequest(), cancellationToken: ct);
            return result?.PublicIds?.ToList() ?? [];
        }
        catch (RpcException ex) { LogGrpcError(ex); return []; }
    }

    public virtual async Task<PagedRecentDiscussionList?> GetSavedDiscussionsAsync(int offset = 0, int pageSize = 20, CancellationToken ct = default)
    {
        try { return await saveClient.GetSavedDiscussionsAsync(new GetSavedDiscussionsRequest { Offset = offset, PageSize = pageSize }, cancellationToken: ct); }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<PagedSavedPostsList?> GetSavedPostsAsync(int offset = 0, int pageSize = 20, CancellationToken ct = default)
    {
        try { return await saveClient.GetSavedPostsAsync(new GetSavedPostsRequest { Offset = offset, PageSize = pageSize }, cancellationToken: ct); }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }

    public virtual async Task<SaveCountsResponse?> GetSaveCountsAsync(CancellationToken ct = default)
    {
        try { return await saveClient.GetSaveCountsAsync(new GetSaveCountsRequest(), cancellationToken: ct); }
        catch (RpcException ex) { LogGrpcError(ex); return null; }
    }
}

// Paged result for moderation responses
public record PagedResult<T>(
    IEnumerable<T> Items,
    int Offset,
    int PageSize,
    bool HasMoreItems);
