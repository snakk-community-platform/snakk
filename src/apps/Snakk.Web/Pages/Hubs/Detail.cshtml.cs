using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Snakk.Web.Helpers;
using Snakk.Web.Pages.ViewModels;
using Snakk.Web.Services;
using Snakk.Protos.Community;
using Snakk.Protos.Discussion;
using Snakk.Protos.Hub;
using Snakk.Protos.Space;

namespace Snakk.Web.Pages.Hubs;

[OutputCache(PolicyName = "Space")]
public class DetailModel(
    SnakkApiClient apiClient,
    IConfiguration configuration,
    ICommunityContext communityContext,
    IPrefetchCacheService prefetchCache) : BasePageModel(configuration, communityContext)
{
    private readonly SnakkApiClient _apiClient = apiClient;

    public HubInfo? Hub { get; set; }
    public HubStats? HubStats { get; set; }
    public CommunityInfo? CommunityDetail { get; set; }
    public PagedSpaceByHubList? Spaces { get; set; }
    public PagedRecentDiscussionList? RecentDiscussions { get; set; }
    public string Slug { get; set; } = string.Empty;

    // Sidebar scope for HTMX partials
    public string SidebarScopeType { get; set; } = "hub";
    public string SidebarScopeId { get; set; } = string.Empty;

    // Trending settings
    public bool ShowTrendingDiscussions => Configuration.GetValue("Trending:SpaceList:ShowDiscussions", true);
    public bool ShowTrendingContributors => Configuration.GetValue("Trending:SpaceList:ShowContributors", true);

    // Banners (bubble-down: hub + community)
    public Snakk.Protos.Banner.BannerList? Banners { get; set; }

    // Inline sidebar data (populated from cache, null = HTMX fallback)
    public SidebarTrendingDiscussionsVM? InlineTrendingDiscussions { get; set; }
    public SidebarTrendingContributorsVM? InlineTrendingContributors { get; set; }
    public SidebarHubRulesVM? InlineHubRules { get; set; }
    public SidebarModeratorsVM? InlineModerators { get; set; }

    public async Task<IActionResult> OnGetAsync(string slug, int offset = 0)
    {
        Slug = slug;

        var hubResult = await _apiClient.GetHubBySlugResultAsync(slug, CommunityContext.CommunitySlug!);
        if (!hubResult.IsSuccess)
            return hubResult.Status == GrpcStatus.NotFound ? NotFound() : StatusCode(503);

        Hub = hubResult.Value!;
        SidebarScopeId = Hub.PublicId;

        // Check cache for sidebar data — inline if warm, prefetch if cold
        ResolveSidebarData();

        var spacesTask = _apiClient.GetSpacesByHubAsync(Hub.PublicId, 0, 50);
        var discussionsTask = _apiClient.GetRecentDiscussionsAsync(offset, 20, hubId: Hub.PublicId);
        var communityTask = !string.IsNullOrEmpty(CommunityContext.CommunitySlug)
            ? _apiClient.GetCommunityBySlugAsync(CommunityContext.CommunitySlug)
            : Task.FromResult<CommunityInfo?>(null);
        var announcementsTask = _apiClient.GetActiveBannersForHubAsync(Hub.PublicId);
        var statsTask = _apiClient.GetHubStatsAsync(Hub.PublicId);

        await Task.WhenAll(spacesTask, discussionsTask, communityTask, announcementsTask, statsTask);

        Spaces = spacesTask.IsCompletedSuccessfully ? spacesTask.Result : null;
        RecentDiscussions = discussionsTask.IsCompletedSuccessfully ? discussionsTask.Result : null;
        CommunityDetail = communityTask.IsCompletedSuccessfully ? communityTask.Result : null;
        Banners = announcementsTask.IsCompletedSuccessfully ? announcementsTask.Result : null;
        HubStats = statsTask.IsCompletedSuccessfully ? statsTask.Result : null;

        // Group access check — only call if hub or its community is restricted
        if (CommunityDetail is not null && (Hub.IsRestricted || CommunityDetail.IsRestricted))
        {
            var access = await _apiClient.CheckGroupAccessAsync(
                CommunityDetail.PublicId,
                Hub.PublicId);

            if (access is not null && !access.CanRead)
                return StatusCode(403);
        }

        return Page();
    }

    private void ResolveSidebarData()
    {
        if (ShowTrendingDiscussions)
            InlineTrendingDiscussions = prefetchCache.ResolveOrPrefetch(
                $"trending-discussions:{SidebarScopeType}:{SidebarScopeId}",
                () => _apiClient.GetTopActiveDiscussionsTodayAsync(Hub!.PublicId),
                d => new SidebarTrendingDiscussionsVM(d, CommunityContext, "cache"));

        if (ShowTrendingContributors)
            InlineTrendingContributors = prefetchCache.ResolveOrPrefetch(
                $"trending-contributors:{SidebarScopeType}:{SidebarScopeId}",
                () => _apiClient.GetTopContributorsTodayAsync(Hub!.PublicId),
                d => new SidebarTrendingContributorsVM(d, CommunityContext, "cache"));

        if (Hub!.HasRules)
            InlineHubRules = prefetchCache.ResolveOrPrefetch(
                $"hub-rules:{SidebarScopeId}",
                () => _apiClient.GetHubRulesAsync(SidebarScopeId),
                d => new SidebarHubRulesVM(
                    d,
                    CommunityContext,
                    CommunityContext.CommunitySlug ?? "",
                    Hub.ParentCommunityHasRules,
                    "cache"));

        InlineModerators = prefetchCache.ResolveOrPrefetch(
            $"moderators:Hub:{SidebarScopeId}",
            () => _apiClient.GetModeratorsAsync("Hub", SidebarScopeId),
            d => new SidebarModeratorsVM(
                d,
                $"{Helpers.SnakkUrlHelper.Hub(CommunityContext, Slug)}/moderators",
                "cache"));
    }
}
