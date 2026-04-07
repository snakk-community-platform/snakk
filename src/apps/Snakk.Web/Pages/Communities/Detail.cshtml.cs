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

    // Sidebar scope for HTMX partials
    public string SidebarScopeType { get; set; } = "community";
    public string SidebarScopeId { get; set; } = string.Empty;

    // Trending settings
    public bool ShowTrendingDiscussions => Configuration.GetValue("Trending:CommunityPage:ShowDiscussions", true);
    public bool ShowTrendingSpaces => Configuration.GetValue("Trending:CommunityPage:ShowSpaces", true);
    public bool ShowTrendingContributors => Configuration.GetValue("Trending:CommunityPage:ShowContributors", true);

    // Banners (community-level only)
    public Snakk.Protos.Banner.BannerList? Banners { get; set; }

    // Inline sidebar data (populated from cache, null = HTMX fallback)
    public SidebarPlatformStatsVM? InlineCommunityStats { get; set; }
    public SidebarTrendingDiscussionsVM? InlineTrendingDiscussions { get; set; }
    public SidebarTrendingSpacesVM? InlineTrendingSpaces { get; set; }
    public SidebarTrendingContributorsVM? InlineTrendingContributors { get; set; }
    public SidebarCommunityRulesVM? InlineCommunityRules { get; set; }
    public SidebarModeratorsVM? InlineModerators { get; set; }

    public async Task<IActionResult> OnGetAsync(string slug, int offset = 0)
    {
        var multiCommunityEnabled = Configuration.GetValue<bool>("Features:MultiCommunityEnabled");
        if (!multiCommunityEnabled)
            return RedirectToPage("/Index");

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

        if (CommunityDetail.HasRules)
            InlineCommunityRules = prefetchCache.ResolveOrPrefetch(
                $"community-rules:{CommunityDetail.PublicId}",
                () => _apiClient.GetCommunityRulesAsync(CommunityDetail.PublicId),
                d => new SidebarCommunityRulesVM(d, "cache"));

        // Check cache for sidebar data — inline if warm, prefetch if cold
        ResolveSidebarData();

        var hubsTask = _apiClient.GetHubsByCommunityAsync(CommunityDetail.PublicId, 0, 50);
        var announcementsTask = _apiClient.GetActiveBannersForCommunityAsync(CommunityDetail.PublicId);
        var statsTask = _apiClient.GetCommunityStatsAsync(CommunityDetail.PublicId);

        await Task.WhenAll(hubsTask, announcementsTask, statsTask);

        Hubs = hubsTask.IsCompletedSuccessfully ? hubsTask.Result : null;
        Banners = announcementsTask.IsCompletedSuccessfully ? announcementsTask.Result : null;

        var stats = statsTask.IsCompletedSuccessfully ? statsTask.Result : null;
        if (stats is not null)
            InlineCommunityStats = new(stats.SpaceCount, stats.DiscussionCount, stats.ReplyCount, "fresh");

        // Fetch spaces for each hub (sequential to avoid DbContext concurrency)
        if (Hubs?.Items != null)
        {
            foreach (var hub in Hubs.Items)
            {
                var spaces = await _apiClient.GetSpacesByHubAsync(hub.PublicId, 0, 50);
                if (spaces != null)
                    SpacesByHub[hub.PublicId] = spaces;
            }
        }

        return Page();
    }

    private void ResolveSidebarData()
    {
        var communityId = CommunityDetail!.PublicId;

        if (ShowTrendingDiscussions)
            InlineTrendingDiscussions = prefetchCache.ResolveOrPrefetch(
                $"trending-discussions:{SidebarScopeType}:{SidebarScopeId}",
                () => _apiClient.GetTopActiveDiscussionsTodayAsync(communityId: communityId),
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
