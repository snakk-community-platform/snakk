using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.OutputCaching;
using Snakk.Web.Pages.ViewModels;
using Snakk.Web.Services;
using Snakk.Protos.Community;
using Snakk.Protos.Hub;
using Snakk.Protos.Space;
using Snakk.Protos.Discussion;

namespace Snakk.Web.Pages.Spaces;

[OutputCache(PolicyName = "Space")]
public class DetailModel(SnakkApiClient apiClient, IConfiguration configuration, ICommunityContext communityContext, IPrefetchCacheService prefetchCache) : BasePageModel(configuration, communityContext)
{
    private readonly SnakkApiClient _apiClient = apiClient;

    public SpaceInfo? Space { get; set; }
    public HubInfo? Hub { get; set; }
    public CommunityInfo? CommunityDetail { get; set; }
    public PagedDiscussionBySpaceList? Discussions { get; set; }
    public SpaceStats? SpaceStats { get; set; }
    public string HubSlug { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public bool IsAuthenticated { get; set; }
    public bool PreferEndlessScroll { get; set; } = true;

    // Sidebar scope for HTMX partials
    public string SidebarScopeType { get; set; } = "space";
    public string SidebarScopeId { get; set; } = string.Empty;

    // Trending settings
    public bool ShowTrendingDiscussions => Configuration.GetValue("Trending:DiscussionList:ShowDiscussions", true);
    public bool ShowTrendingContributors => Configuration.GetValue("Trending:DiscussionList:ShowContributors", true);

    // Inline sidebar data (populated from cache, null = HTMX fallback)
    public SidebarTrendingDiscussionsVM? InlineTrendingDiscussions { get; set; }
    public SidebarTrendingContributorsVM? InlineTrendingContributors { get; set; }
    public SidebarSpaceRulesVM? InlineSpaceRules { get; set; }

    public async Task<IActionResult> OnGetAsync(string hubSlug, string slug, int offset = 0)
    {
        HubSlug = hubSlug;
        Slug = slug;

        // Read preferences from cookies (no API call needed)
        IsAuthenticated = HttpContext.Request.Cookies.ContainsKey(AuthCookieHelper.AccessCookieName);
        PreferEndlessScroll = AuthCookieHelper.GetPreferEndlessScroll(HttpContext);

        // Fetch hub, space, and community in parallel
        var hubTask = _apiClient.GetHubBySlugAsync(hubSlug);
        var spaceTask = _apiClient.GetSpaceBySlugAsync(slug);
        var communityTask = !string.IsNullOrEmpty(CommunityContext.CommunitySlug)
            ? _apiClient.GetCommunityBySlugAsync(CommunityContext.CommunitySlug)
            : Task.FromResult<CommunityInfo?>(null);

        await Task.WhenAll(hubTask, spaceTask, communityTask);

        Hub = hubTask.Result;
        Space = spaceTask.Result;
        CommunityDetail = communityTask.IsCompletedSuccessfully ? communityTask.Result : null;

        if (Space == null)
            return NotFound();

        SidebarScopeId = Space.PublicId;

        // Check cache for sidebar data — inline if warm, prefetch if cold
        ResolveSidebarData();

        // Fetch discussions and stats in parallel
        var discussionsTask = _apiClient.GetDiscussionsBySpaceAsync(Space.PublicId, offset, 20);
        var statsTask = _apiClient.GetSpaceStatsAsync(Space.PublicId);

        await Task.WhenAll(discussionsTask, statsTask);

        Discussions = discussionsTask.IsCompletedSuccessfully ? discussionsTask.Result : null;
        SpaceStats = statsTask.IsCompletedSuccessfully ? statsTask.Result : null;

        return Page();
    }

    private void ResolveSidebarData()
    {
        if (ShowTrendingDiscussions)
            InlineTrendingDiscussions = prefetchCache.ResolveOrPrefetch($"trending-discussions:{SidebarScopeType}:{SidebarScopeId}",
                () => _apiClient.GetTopActiveDiscussionsTodayAsync(spaceId: SidebarScopeId), d => new SidebarTrendingDiscussionsVM(d, CommunityContext, "cache"));

        if (ShowTrendingContributors)
            InlineTrendingContributors = prefetchCache.ResolveOrPrefetch($"trending-contributors:{SidebarScopeType}:{SidebarScopeId}",
                () => _apiClient.GetTopContributorsTodayAsync(spaceId: SidebarScopeId), d => new SidebarTrendingContributorsVM(d, CommunityContext, "cache"));

        InlineSpaceRules = prefetchCache.ResolveOrPrefetch($"space-rules:{SidebarScopeId}",
            () => _apiClient.GetSpaceRulesAsync(SidebarScopeId), d => new SidebarSpaceRulesVM(d, CommunityContext, HubSlug, CommunityContext.CommunitySlug ?? "", Space?.ParentHubHasRules ?? false, Space?.ParentCommunityHasRules ?? false, "cache"));
    }
}
