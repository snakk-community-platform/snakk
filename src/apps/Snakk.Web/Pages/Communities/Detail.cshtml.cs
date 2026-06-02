using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Snakk.Web.Helpers;
using Snakk.Web.Pages.ViewModels;
using Snakk.Web.Services;
using Snakk.Protos.Community;
using Snakk.Protos.Discussion;
using Snakk.Protos.Hub;
using Snakk.Protos.Space;

namespace Snakk.Web.Pages.Communities;

[OutputCache(PolicyName = "AnonymousPage")]
public class DetailModel(
    SnakkApiClient apiClient,
    IConfiguration configuration,
    ICommunityContext communityContext,
    IPrefetchCacheService prefetchCache) : BasePageModel(configuration, communityContext)
{
    private readonly SnakkApiClient _apiClient = apiClient;

    public CommunityInfo? CommunityDetail { get; set; }
    public PagedHubList? Hubs { get; set; }
    public Dictionary<string, PagedSpaceByHubList> SpacesByHub { get; set; } = new();
    public Dictionary<string, string> SpaceSparklineJson { get; set; } = new();

    // Sidebar scope for HTMX partials
    public string SidebarScopeType { get; set; } = "community";
    public string SidebarScopeId { get; set; } = string.Empty;

    // Trending settings
    public bool ShowTrendingDiscussions => Configuration.GetValue("Trending:CommunityPage:ShowDiscussions", true);
    public bool ShowTrendingSpaces => Configuration.GetValue("Trending:CommunityPage:ShowSpaces", true);
    public bool ShowTrendingContributors => Configuration.GetValue("Trending:CommunityPage:ShowContributors", true);

    // Banners (community-level only)
    public Snakk.Protos.Banner.BannerList? Banners { get; set; }

    // 2FA gating: true when the community requires 2FA and the visitor doesn't have it enabled.
    public bool Require2FAGate { get; set; }

    // Inline sidebar data (populated from cache, null = HTMX fallback)
    public SidebarPlatformStatsVM? InlineCommunityStats { get; set; }
    public SidebarTrendingDiscussionsVM? InlineTrendingDiscussions { get; set; }
    public SidebarTrendingSpacesVM? InlineTrendingSpaces { get; set; }
    public SidebarTrendingContributorsVM? InlineTrendingContributors { get; set; }
    public SidebarCommunityRulesVM? InlineCommunityRules { get; set; }
    public SidebarModeratorsVM? InlineModerators { get; set; }

    public async Task<IActionResult> OnGetAsync(string slug, int offset = 0, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var communityResult = await _apiClient.GetCommunityBySlugResultAsync(slug);

        if (!communityResult.IsSuccess)
            return communityResult.Status == GrpcStatus.NotFound ? NotFound() : StatusCode(503);

        CommunityDetail = communityResult.Value!;
        SidebarScopeId = CommunityDetail.PublicId;

        // Group access check — only call if the community is actually restricted
        if (CommunityDetail.IsRestricted)
        {
            var communityAccess = await _apiClient.CheckGroupAccessAsync(CommunityDetail.PublicId);
            if (communityAccess is not null && communityAccess.AccessLevel < 1)
                return StatusCode(403);
        }

        // 2FA gate — short-circuit if the community requires 2FA and the visitor doesn't have it
        if (CommunityDetail.Require2Fa && User.FindFirst("tfa")?.Value != "1")
        {
            Require2FAGate = true;
            return Page();
        }

        if (CommunityDetail.HasRules)
            InlineCommunityRules = prefetchCache.ResolveOrPrefetch(
                $"community-rules:{CommunityDetail.PublicId}",
                () => _apiClient.GetCommunityRulesAsync(CommunityDetail.PublicId),
                d => new SidebarCommunityRulesVM(d, "cache"));

        var viewerAllowsAdult = await AdultContentGate.ViewerAllowsAdultAsync(HttpContext, _apiClient);

        // Check cache for sidebar data — inline if warm, prefetch if cold
        ResolveSidebarData(viewerAllowsAdult);

        var hubsTask = _apiClient.GetHubsByCommunityAsync(CommunityDetail.PublicId, 0, 50);
        var announcementsTask = _apiClient.GetActiveBannersForCommunityAsync(CommunityDetail.PublicId);
        var statsTask = _apiClient.GetCommunityStatsAsync(CommunityDetail.PublicId);

        await Task.WhenAll(hubsTask, announcementsTask, statsTask);

        Hubs = hubsTask.IsCompletedSuccessfully ? hubsTask.Result : null;
        Banners = announcementsTask.IsCompletedSuccessfully ? announcementsTask.Result : null;

        var stats = statsTask.IsCompletedSuccessfully ? statsTask.Result : null;
        if (stats is not null)
            InlineCommunityStats = new(stats.SpaceCount, stats.DiscussionCount, stats.ReplyCount, "fresh");

        // Fetch spaces for all hubs in parallel (each gRPC call has its own DbContext scope)
        if (Hubs?.Items != null)
        {
            var spaceTasks = Hubs.Items.Select(async hub =>
                (hub.PublicId, spaces: await _apiClient.GetSpacesByHubAsync(hub.PublicId, 0, 50)));
            foreach (var (hubPublicId, spaces) in await Task.WhenAll(spaceTasks))
                if (spaces != null)
                    SpacesByHub[hubPublicId] = spaces;
        }

        // Fetch sparklines for all spaces in a single batch gRPC call (2 DB queries total)
        var allSpaceIds = SpacesByHub.Values.SelectMany(s => s.Items).Select(s => s.PublicId).ToList();
        if (allSpaceIds.Count > 0)
        {
            var batchResult = await _apiClient.GetActivitySparklinesBatchAsync(allSpaceIds, 7);
            if (batchResult != null)
            {
                foreach (var entry in batchResult.Entries)
                {
                    if (entry.Days.Count > 0)
                        SpaceSparklineJson[entry.PublicId] = System.Text.Json.JsonSerializer.Serialize(
                            entry.Days.Select(d => new { date = d.Date, postCount = d.PostCount, discussionCount = d.DiscussionCount }));
                }
            }
        }

        return Page();
    }

    private void ResolveSidebarData(bool viewerAllowsAdult)
    {
        var communityId = CommunityDetail!.PublicId;
        var adultSuffix = viewerAllowsAdult ? "adult" : "safe";

        if (ShowTrendingDiscussions)
            InlineTrendingDiscussions = prefetchCache.ResolveOrPrefetch(
                $"trending-discussions:{SidebarScopeType}:{SidebarScopeId}:{adultSuffix}",
                () => _apiClient.GetTopActiveDiscussionsTodayAsync(communityId: communityId, viewerAllowsAdult: viewerAllowsAdult),
                d => new SidebarTrendingDiscussionsVM(d, CommunityContext, "cache"));

        if (ShowTrendingSpaces)
            InlineTrendingSpaces = prefetchCache.ResolveOrPrefetch(
                $"trending-spaces:{SidebarScopeType}:{SidebarScopeId}",
                () => _apiClient.GetTopActiveSpacesTodayAsync(communityId: communityId),
                d => new SidebarTrendingSpacesVM(d, CommunityContext, "cache"));

        if (ShowTrendingContributors)
            InlineTrendingContributors = prefetchCache.ResolveOrPrefetch(
                $"trending-contributors:{SidebarScopeType}:{SidebarScopeId}",
                () => _apiClient.GetTopContributorsTodayAsync(communityId: communityId),
                d => new SidebarTrendingContributorsVM(d, CommunityContext, "cache"));

        InlineModerators = prefetchCache.ResolveOrPrefetch(
            $"moderators:Community:{SidebarScopeId}",
            () => _apiClient.GetModeratorsAsync("Community", SidebarScopeId),
            d => new SidebarModeratorsVM(
                d,
                $"{Helpers.SnakkUrlHelper.Community(CommunityDetail!.Slug, CommunityContext)}/moderators",
                "cache"));
    }
}
