using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.OutputCaching;
using Snakk.Web.Pages.ViewModels;
using Snakk.Web.Services;
using Snakk.Protos.Hub;
using Snakk.Protos.Community;
using Snakk.Protos.Statistics;

namespace Snakk.Web.Pages.Hubs;

[OutputCache(PolicyName = "HomePage")]
public class IndexModel(
    SnakkApiClient apiClient,
    IConfiguration configuration,
    ICommunityContext communityContext,
    IPrefetchCacheService prefetchCache) : PageModel
{
    private readonly SnakkApiClient _apiClient = apiClient;

    public PagedHubList? Hubs { get; set; }
    public CommunityInfo? CommunityDetail { get; set; }
    public PlatformStats? PlatformStats { get; set; }
    public CommunityStats? CommunityStats { get; set; }
    public string ApiBaseUrl => configuration["ApiBaseUrl"] ?? "https://localhost:17100";
    public ICommunityContext Community => communityContext;

    // Sidebar scope for HTMX partials
    public string SidebarScopeType { get; set; } = "platform";
    public string SidebarScopeId { get; set; } = "global";

    // Stats accessor that works for both platform and community-scoped views
    public int HubCount => CommunityStats?.HubCount ?? PlatformStats?.HubCount ?? 0;
    public int SpaceCount => CommunityStats?.SpaceCount ?? PlatformStats?.SpaceCount ?? 0;
    public int DiscussionCount => CommunityStats?.DiscussionCount ?? PlatformStats?.DiscussionCount ?? 0;
    public int ReplyCount => CommunityStats?.ReplyCount ?? PlatformStats?.ReplyCount ?? 0;

    // Trending settings
    public bool ShowTrendingDiscussions => configuration.GetValue("Trending:HubList:ShowDiscussions", true);
    public bool ShowTrendingSpaces => configuration.GetValue("Trending:HubList:ShowSpaces", true);
    public bool ShowTrendingContributors => configuration.GetValue("Trending:HubList:ShowContributors", true);

    // Whether to show community in breadcrumb (multi-community enabled, non-default community, not on custom domain)
    public bool ShowCommunityInBreadcrumb =>
        configuration.GetValue<bool>("Features:MultiCommunityEnabled")
        && !communityContext.IsDefaultCommunity
        && !communityContext.IsCustomDomain;

    // Inline sidebar data (populated from cache, null = HTMX fallback)
    public SidebarTrendingSpacesVM? InlineTrendingSpaces { get; set; }
    public SidebarTrendingDiscussionsVM? InlineTrendingDiscussions { get; set; }
    public SidebarTrendingContributorsVM? InlineTrendingContributors { get; set; }

    public async Task OnGetAsync(int offset = 0)
    {
        // Fetch community detail for breadcrumb popup or custom domain scoping
        string? communityId = null;
        if (!string.IsNullOrEmpty(communityContext.CommunitySlug)
            && (communityContext.IsCustomDomain || ShowCommunityInBreadcrumb))
        {
            var community = await _apiClient.GetCommunityBySlugAsync(communityContext.CommunitySlug);
            CommunityDetail = community;
            if (communityContext.IsCustomDomain)
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

        // Use community-scoped hub list if on custom domain, otherwise all hubs
        Task<PagedHubList?> hubsTask;
        if (!string.IsNullOrEmpty(communityId))
            hubsTask = _apiClient.GetHubsByCommunityAsync(communityId, offset, 20);
        else
            hubsTask = _apiClient.GetHubsAsync(offset, 20);

        var tasks = new List<Task> { hubsTask };

        // Use community stats if on custom domain, otherwise platform stats
        Task<CommunityStats?>? communityStatsTask = null;
        Task<PlatformStats?>? platformStatsTask = null;

        if (!string.IsNullOrEmpty(communityId))
        {
            communityStatsTask = _apiClient.GetCommunityStatsAsync(communityId);
            tasks.Add(communityStatsTask);
        }
        else
        {
            platformStatsTask = _apiClient.GetPlatformStatsAsync();
            tasks.Add(platformStatsTask);
        }

        await Task.WhenAll(tasks);

        Hubs = hubsTask.IsCompletedSuccessfully ? hubsTask.Result : null;
        PlatformStats = platformStatsTask?.IsCompletedSuccessfully == true ? platformStatsTask.Result : null;
        CommunityStats = communityStatsTask?.IsCompletedSuccessfully == true ? communityStatsTask.Result : null;
    }

    private void ResolveSidebarData(string? communityId)
    {
        if (ShowTrendingSpaces)
            InlineTrendingSpaces = prefetchCache.ResolveOrPrefetch(
                $"trending-spaces:{SidebarScopeType}:{SidebarScopeId}",
                () => _apiClient.GetTopActiveSpacesTodayAsync(communityId: communityId),
                d => new SidebarTrendingSpacesVM(d, communityContext, "cache"));

        if (ShowTrendingDiscussions)
            InlineTrendingDiscussions = prefetchCache.ResolveOrPrefetch(
                $"trending-discussions:{SidebarScopeType}:{SidebarScopeId}",
                () => _apiClient.GetTopActiveDiscussionsTodayAsync(communityId: communityId),
                d => new SidebarTrendingDiscussionsVM(d, communityContext, "cache"));

        if (ShowTrendingContributors)
            InlineTrendingContributors = prefetchCache.ResolveOrPrefetch(
                $"trending-contributors:{SidebarScopeType}:{SidebarScopeId}",
                () => _apiClient.GetTopContributorsTodayAsync(communityId: communityId),
                d => new SidebarTrendingContributorsVM(d, communityContext, "cache"));
    }
}
