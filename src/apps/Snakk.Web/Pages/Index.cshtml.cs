using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.OutputCaching;
using Snakk.Web.Pages.ViewModels;
using Snakk.Web.Services;
using Snakk.Protos.Discussion;

namespace Snakk.Web.Pages;

[OutputCache(PolicyName = "AnonymousPage")]
public class IndexModel(
    SnakkApiClient apiClient,
    IConfiguration configuration,
    ICommunityContext communityContext,
    IPrefetchCacheService prefetchCache,
    ILogger<IndexModel> logger) : BasePageModel(configuration, communityContext)
{
    private readonly SnakkApiClient _apiClient = apiClient;

    public PagedRecentDiscussionList? RecentDiscussions { get; set; }

    // Sidebar scope for HTMX partials
    public string SidebarScopeType { get; set; } = "platform";
    public string SidebarScopeId { get; set; } = "global";

    // Trending settings
    public bool ShowTrendingDiscussions => Configuration.GetValue("Trending:FrontPage:ShowDiscussions", true);
    public bool ShowTrendingSpaces => Configuration.GetValue("Trending:FrontPage:ShowSpaces", true);
    public bool ShowTrendingContributors => Configuration.GetValue("Trending:FrontPage:ShowContributors", true);

    // Whether to show community in discussion list (multi-community, no specific community scoped, not on custom domain)
    public bool ShowCommunityInDiscussionList =>
        CommunityContext.IsMultiCommunityEnabled
        && string.IsNullOrEmpty(CommunityContext.CommunitySlug)
        && !CommunityContext.IsCustomDomain;

    // Site rules revision for cache-busting HTMX URL
    public string SiteRulesRevision { get; set; } = "";

    // Inline sidebar data (populated from cache, null = HTMX fallback)
    public SidebarPlatformStatsVM? InlinePlatformStats { get; set; }
    public SidebarSiteRulesVM? InlineSiteRules { get; set; }
    public SidebarTrendingDiscussionsVM? InlineTrendingDiscussions { get; set; }
    public SidebarTrendingSpacesVM? InlineTrendingSpaces { get; set; }
    public SidebarTrendingContributorsVM? InlineTrendingContributors { get; set; }


    public async Task OnGetAsync(int offset = 0)
    {
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

        // Eagerly fetch any sidebar data that wasn't in cache
        await EnsureSidebarDataAsync(communityId);

        try
        {
            RecentDiscussions = await _apiClient.GetRecentDiscussionsAsync(offset, 20, communityId);
        }
        catch
        {
            // Continue with null
        }

    }

    private async Task EnsureSidebarDataAsync(string? communityId)
    {
        var tasks = new List<Task>();

        if (InlinePlatformStats is null)
        {
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    if (!string.IsNullOrEmpty(communityId))
                    {
                        var data = await _apiClient.GetCommunityStatsAsync(communityId);
                        if (data is not null)
                            InlinePlatformStats = new(data.SpaceCount, data.DiscussionCount, data.ReplyCount, "eager");
                    }
                    else
                    {
                        var data = await _apiClient.GetPlatformStatsAsync();
                        if (data is not null)
                            InlinePlatformStats = new(data.SpaceCount, data.DiscussionCount, data.ReplyCount, "eager");
                    }
                }
                catch (Exception ex) { logger.LogWarning(ex, "Failed to fetch platform stats"); }
            }));
        }

        if (ShowTrendingDiscussions && InlineTrendingDiscussions is null)
        {
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    var data = await _apiClient.GetTopActiveDiscussionsTodayAsync(communityId: communityId);
                    if (data is not null)
                        InlineTrendingDiscussions = new(data, CommunityContext, "eager");
                }
                catch (Exception ex) { logger.LogWarning(ex, "Failed to fetch trending discussions"); }
            }));
        }

        if (ShowTrendingSpaces && InlineTrendingSpaces is null)
        {
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    var data = await _apiClient.GetTopActiveSpacesTodayAsync(communityId: communityId);
                    if (data is not null)
                        InlineTrendingSpaces = new(data, CommunityContext, "eager");
                }
                catch (Exception ex) { logger.LogWarning(ex, "Failed to fetch trending spaces"); }
            }));
        }

        if (ShowTrendingContributors && InlineTrendingContributors is null)
        {
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    var data = await _apiClient.GetTopContributorsTodayAsync(communityId: communityId);
                    if (data is not null)
                        InlineTrendingContributors = new(data, CommunityContext, "eager");
                }
                catch (Exception ex) { logger.LogWarning(ex, "Failed to fetch trending contributors"); }
            }));
        }

        if (InlineSiteRules is null)
        {
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    var data = await _apiClient.GetSiteRulesAsync();
                    if (data is not null)
                        InlineSiteRules = new(data, "eager");
                }
                catch (Exception ex) { logger.LogWarning(ex, "Failed to fetch site rules"); }
            }));
        }

        await Task.WhenAll(tasks);
    }

    private void ResolveSidebarData(string? communityId)
    {
        // Platform stats (two source types → mapped to one VM)
        if (!string.IsNullOrEmpty(communityId))
        {
            var data = prefetchCache.ResolveOrPrefetch(
                $"platform-stats:community:{communityId}",
                () => _apiClient.GetCommunityStatsAsync(communityId));
            if (data is not null)
                InlinePlatformStats = new(data.SpaceCount, data.DiscussionCount, data.ReplyCount, "cache");
        }
        else
        {
            var data = prefetchCache.ResolveOrPrefetch(
                "platform-stats:platform:global",
                () => _apiClient.GetPlatformStatsAsync());
            if (data is not null)
                InlinePlatformStats = new(data.SpaceCount, data.DiscussionCount, data.ReplyCount, "cache");
        }

        InlineSiteRules = prefetchCache.ResolveOrPrefetch(
            "site-rules",
            () => _apiClient.GetSiteRulesAsync(),
            d => new SidebarSiteRulesVM(d, "cache"));
        SiteRulesRevision = InlineSiteRules?.Rules?.Revision ?? "";

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
    }
}
