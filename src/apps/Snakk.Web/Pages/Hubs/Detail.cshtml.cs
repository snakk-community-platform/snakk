using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.OutputCaching;
using Snakk.Web.Pages.ViewModels;
using Snakk.Web.Services;
using Snakk.Protos.Community;
using Snakk.Protos.Hub;
using Snakk.Protos.Space;

namespace Snakk.Web.Pages.Hubs;

[OutputCache(PolicyName = "Space")]
public class DetailModel(
    SnakkApiClient apiClient,
    IConfiguration configuration,
    ICommunityContext communityContext,
    IPrefetchCacheService prefetchCache) : PageModel
{
    private readonly SnakkApiClient _apiClient = apiClient;

    public HubInfo? Hub { get; set; }
    public CommunityInfo? CommunityDetail { get; set; }
    public PagedSpaceByHubList? Spaces { get; set; }
    public HubStats? HubStats { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string ApiBaseUrl => configuration["ApiBaseUrl"] ?? "https://localhost:17100";
    public ICommunityContext Community => communityContext;

    // Sidebar scope for HTMX partials
    public string SidebarScopeType { get; set; } = "hub";
    public string SidebarScopeId { get; set; } = string.Empty;

    // Trending settings
    public bool ShowTrendingDiscussions => configuration.GetValue("Trending:SpaceList:ShowDiscussions", true);
    public bool ShowTrendingContributors => configuration.GetValue("Trending:SpaceList:ShowContributors", true);

    // Whether to show community in breadcrumb (multi-community enabled, non-default community, not on custom domain)
    public bool ShowCommunityInBreadcrumb =>
        configuration.GetValue<bool>("Features:MultiCommunityEnabled")
        && !communityContext.IsDefaultCommunity
        && !communityContext.IsCustomDomain;

    // Inline sidebar data (populated from cache, null = HTMX fallback)
    public SidebarTrendingDiscussionsVM? InlineTrendingDiscussions { get; set; }
    public SidebarTrendingContributorsVM? InlineTrendingContributors { get; set; }
    public SidebarHubRulesVM? InlineHubRules { get; set; }

    public async Task<IActionResult> OnGetAsync(string slug, int offset = 0)
    {
        Slug = slug;

        var hubResult = await _apiClient.GetHubBySlugResultAsync(slug);
        if (!hubResult.IsSuccess)
            return hubResult.Status == GrpcStatus.NotFound ? NotFound() : StatusCode(503);

        Hub = hubResult.Value!;

        SidebarScopeId = Hub.PublicId;

        // Check cache for sidebar data — inline if warm, prefetch if cold
        ResolveSidebarData();

        var spacesTask = _apiClient.GetSpacesByHubAsync(Hub.PublicId, offset, 20);
        var statsTask = _apiClient.GetHubStatsAsync(Hub.PublicId);
        var communityTask = !string.IsNullOrEmpty(communityContext.CommunitySlug)
            ? _apiClient.GetCommunityBySlugAsync(communityContext.CommunitySlug)
            : Task.FromResult<CommunityInfo?>(null);

        await Task.WhenAll(spacesTask, statsTask, communityTask);

        Spaces = spacesTask.IsCompletedSuccessfully ? spacesTask.Result : null;
        HubStats = statsTask.IsCompletedSuccessfully ? statsTask.Result : null;
        CommunityDetail = communityTask.IsCompletedSuccessfully ? communityTask.Result : null;

        return Page();
    }

    private void ResolveSidebarData()
    {
        if (ShowTrendingDiscussions)
            InlineTrendingDiscussions = prefetchCache.ResolveOrPrefetch(
                $"trending-discussions:{SidebarScopeType}:{SidebarScopeId}",
                () => _apiClient.GetTopActiveDiscussionsTodayAsync(Hub!.PublicId),
                d => new SidebarTrendingDiscussionsVM(d, communityContext, "cache"));

        if (ShowTrendingContributors)
            InlineTrendingContributors = prefetchCache.ResolveOrPrefetch(
                $"trending-contributors:{SidebarScopeType}:{SidebarScopeId}",
                () => _apiClient.GetTopContributorsTodayAsync(Hub!.PublicId),
                d => new SidebarTrendingContributorsVM(d, communityContext, "cache"));

        if (Hub!.HasRules)
            InlineHubRules = prefetchCache.ResolveOrPrefetch(
                $"hub-rules:{SidebarScopeId}",
                () => _apiClient.GetHubRulesAsync(SidebarScopeId),
                d => new SidebarHubRulesVM(
                    d,
                    communityContext,
                    communityContext.CommunitySlug ?? "",
                    Hub.ParentCommunityHasRules,
                    "cache"));
    }
}
