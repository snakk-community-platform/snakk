using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.OutputCaching;
using Snakk.Web.Pages.ViewModels;
using Snakk.Web.Services;
using Snakk.Protos.Discussion;

namespace Snakk.Web.Pages;

[OutputCache(PolicyName = "HomePage")]
public class IndexModel(SnakkApiClient apiClient, IConfiguration configuration, ICommunityContext communityContext, IPrefetchCacheService prefetchCache) : BasePageModel(configuration, communityContext)
{
    private readonly SnakkApiClient _apiClient = apiClient;

    public PagedRecentDiscussionList? RecentDiscussions { get; set; }
    public bool PreferEndlessScroll { get; set; } = true;

    // Sidebar scope for HTMX partials
    public string SidebarScopeType { get; set; } = "platform";
    public string SidebarScopeId { get; set; } = "global";

    // Trending settings
    public bool ShowTrendingDiscussions => Configuration.GetValue("Trending:FrontPage:ShowDiscussions", true);
    public bool ShowTrendingSpaces => Configuration.GetValue("Trending:FrontPage:ShowSpaces", true);
    public bool ShowTrendingContributors => Configuration.GetValue("Trending:FrontPage:ShowContributors", true);

    // Whether to show community in discussion list (multi-community enabled, default community, not on custom domain)
    public bool ShowCommunityInDiscussionList =>
        Configuration.GetValue<bool>("Features:MultiCommunityEnabled") &&
        CommunityContext.IsDefaultCommunity &&
        !CommunityContext.IsCustomDomain;

    // Inline sidebar data (populated from cache, null = HTMX fallback)
    public SidebarPlatformStatsVM? InlinePlatformStats { get; set; }
    public SidebarTrendingDiscussionsVM? InlineTrendingDiscussions { get; set; }
    public SidebarTrendingSpacesVM? InlineTrendingSpaces { get; set; }
    public SidebarTrendingContributorsVM? InlineTrendingContributors { get; set; }

    public async Task OnGetAsync(int offset = 0)
    {
        // Read preference from cookie (set by /bff/me on page load)
        PreferEndlessScroll = AuthCookieHelper.GetPreferEndlessScroll(HttpContext);

        // Determine if we need to scope to a community
        string? communityId = null;
        if (CommunityContext.IsCustomDomain && !string.IsNullOrEmpty(CommunityContext.CommunitySlug))
        {
            // Get the community to retrieve its public ID
            var community = await _apiClient.GetCommunityBySlugAsync(CommunityContext.CommunitySlug);
            communityId = community?.PublicId;
        }

        // Set sidebar scope for HTMX partials
        if (!string.IsNullOrEmpty(communityId))
        {
            SidebarScopeType = "community";
            SidebarScopeId = communityId;
        }

        // Check cache for sidebar data — inline if warm, prefetch if cold
        ResolveSidebarData(communityId);

        try
        {
            RecentDiscussions = await _apiClient.GetRecentDiscussionsAsync(offset, 20, communityId);
        }
        catch
        {
            // Continue with null
        }
    }

    private void ResolveSidebarData(string? communityId)
    {
        // Platform stats (two source types → mapped to one VM)
        if (!string.IsNullOrEmpty(communityId))
        {
            var data = prefetchCache.ResolveOrPrefetch($"platform-stats:community:{communityId}", () => _apiClient.GetCommunityStatsAsync(communityId));
            if (data != null)
                InlinePlatformStats = new(data.SpaceCount, data.DiscussionCount, data.ReplyCount, "cache");
        }
        else
        {
            var data = prefetchCache.ResolveOrPrefetch("platform-stats:platform:global", () => _apiClient.GetPlatformStatsAsync());
            if (data != null)
                InlinePlatformStats = new(data.SpaceCount, data.DiscussionCount, data.ReplyCount, "cache");
        }

        if (ShowTrendingDiscussions)
            InlineTrendingDiscussions = prefetchCache.ResolveOrPrefetch($"trending-discussions:{SidebarScopeType}:{SidebarScopeId}",
                () => _apiClient.GetTopActiveDiscussionsTodayAsync(communityId: communityId), d => new SidebarTrendingDiscussionsVM(d, CommunityContext, "cache"));

        if (ShowTrendingSpaces)
            InlineTrendingSpaces = prefetchCache.ResolveOrPrefetch($"trending-spaces:{SidebarScopeType}:{SidebarScopeId}",
                () => _apiClient.GetTopActiveSpacesTodayAsync(communityId: communityId), d => new SidebarTrendingSpacesVM(d, CommunityContext, "cache"));

        if (ShowTrendingContributors)
            InlineTrendingContributors = prefetchCache.ResolveOrPrefetch($"trending-contributors:{SidebarScopeType}:{SidebarScopeId}",
                () => _apiClient.GetTopContributorsTodayAsync(communityId: communityId), d => new SidebarTrendingContributorsVM(d, CommunityContext, "cache"));
    }
}
