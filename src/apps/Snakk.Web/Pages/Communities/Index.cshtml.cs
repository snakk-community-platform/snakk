using Microsoft.AspNetCore.Mvc;
using Snakk.Web.Pages.ViewModels;
using Snakk.Web.Services;
using Snakk.Protos.Community;

namespace Snakk.Web.Pages.Communities;

public class IndexModel(
    SnakkApiClient apiClient,
    IConfiguration configuration,
    ICommunityContext communityContext,
    IPrefetchCacheService prefetchCache) : BasePageModel(configuration, communityContext)
{
    private readonly SnakkApiClient _apiClient = apiClient;

    public PagedCommunityList? Communities { get; set; }
    public Dictionary<string, string> CommunitySparklineJson { get; set; } = new();

    public string SidebarScopeType { get; set; } = "platform";
    public string SidebarScopeId { get; set; } = "global";

    public bool ShowTrendingDiscussions => Configuration.GetValue("Trending:FrontPage:ShowDiscussions", true);
    public bool ShowTrendingSpaces => Configuration.GetValue("Trending:FrontPage:ShowSpaces", true);
    public bool ShowTrendingContributors => Configuration.GetValue("Trending:FrontPage:ShowContributors", true);

    public string SiteRulesRevision { get; set; } = "";

    public SidebarPlatformStatsVM? InlinePlatformStats { get; set; }
    public SidebarSiteRulesVM? InlineSiteRules { get; set; }
    public SidebarTrendingDiscussionsVM? InlineTrendingDiscussions { get; set; }
    public SidebarTrendingSpacesVM? InlineTrendingSpaces { get; set; }
    public SidebarTrendingContributorsVM? InlineTrendingContributors { get; set; }

    public async Task<IActionResult> OnGetAsync(int offset = 0, CancellationToken cancellationToken = default)
    {
        Preload("community");
        cancellationToken.ThrowIfCancellationRequested();
        if (!CommunityContext.IsMultiCommunityEnabled)
            return RedirectToPage("/Index");

        var viewerAllowsAdult = await AdultContentGate.ViewerAllowsAdultAsync(HttpContext, _apiClient);
        ResolveSidebarData(viewerAllowsAdult);

        Communities = await _apiClient.GetCommunitiesAsync(offset, 20);

        if (Communities?.Items is { Count: > 0 } communityItems)
        {
            var communityPublicIds = communityItems.Select(c => c.PublicId).ToList();
            var batchResult = await _apiClient.GetActivitySparklinesBatchAsync(communityPublicIds, days: 7, entityType: "community");
            if (batchResult is not null)
            {
                foreach (var entry in batchResult.Entries)
                {
                    if (entry.Days.Count > 0)
                        CommunitySparklineJson[entry.PublicId] = System.Text.Json.JsonSerializer.Serialize(
                            entry.Days.Select(d => new { date = d.Date, postCount = d.PostCount, discussionCount = d.DiscussionCount }));
                }
            }
        }

        return Page();
    }

    private void ResolveSidebarData(bool viewerAllowsAdult)
    {
        var adultSuffix = viewerAllowsAdult ? "adult" : "safe";
        var data = prefetchCache.ResolveOrPrefetch(
            "platform-stats:platform:global",
            () => _apiClient.GetPlatformStatsAsync());
        if (data is not null)
            InlinePlatformStats = new(data.SpaceCount, data.DiscussionCount, data.ReplyCount, "cache");

        InlineSiteRules = prefetchCache.ResolveOrPrefetch(
            "site-rules",
            () => _apiClient.GetSiteRulesAsync(),
            d => new SidebarSiteRulesVM(d, "cache"));
        SiteRulesRevision = InlineSiteRules?.Rules?.Revision ?? "";

        if (ShowTrendingDiscussions)
            InlineTrendingDiscussions = prefetchCache.ResolveOrPrefetch(
                $"trending-discussions:platform:global:{adultSuffix}",
                () => _apiClient.GetTopActiveDiscussionsTodayAsync(viewerAllowsAdult: viewerAllowsAdult),
                d => new SidebarTrendingDiscussionsVM(d, CommunityContext, "cache"));

        if (ShowTrendingSpaces)
            InlineTrendingSpaces = prefetchCache.ResolveOrPrefetch(
                "trending-spaces:platform:global",
                () => _apiClient.GetTopActiveSpacesTodayAsync(),
                d => new SidebarTrendingSpacesVM(d, CommunityContext, "cache"));

        if (ShowTrendingContributors)
            InlineTrendingContributors = prefetchCache.ResolveOrPrefetch(
                "trending-contributors:platform:global",
                () => _apiClient.GetTopContributorsTodayAsync(),
                d => new SidebarTrendingContributorsVM(d, CommunityContext, "cache"));
    }
}
