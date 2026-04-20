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

    public string SidebarScopeType { get; set; } = "platform";
    public string SidebarScopeId { get; set; } = "global";

    public bool ShowCommunityInDiscussionList =>
        CommunityContext.IsMultiCommunityEnabled
        && string.IsNullOrEmpty(CommunityContext.CommunitySlug)
        && !CommunityContext.IsCustomDomain;

    public SidebarPlatformStatsVM? InlinePlatformStats { get; set; }
    public SidebarTrendingSpacesVM? InlineTrendingSpaces { get; set; }
    public SidebarTrendingContributorsVM? InlineTrendingContributors { get; set; }

    public async Task OnGetAsync(int offset = 0)
    {
        string? communityId = null;
        if (CommunityContext.IsCustomDomain && !string.IsNullOrEmpty(CommunityContext.CommunitySlug))
        {
            var community = await _apiClient.GetCommunityBySlugAsync(CommunityContext.CommunitySlug);
            communityId = community?.PublicId;
        }

        if (!string.IsNullOrEmpty(communityId))
        {
            SidebarScopeType = "community";
            SidebarScopeId = communityId;
        }

        ResolveSidebarData(communityId);
        await EnsureSidebarDataAsync(communityId);

        try { RecentDiscussions = await _apiClient.GetRecentDiscussionsAsync(offset, 20, communityId); }
        catch { }
    }

    private async Task EnsureSidebarDataAsync(string? communityId)
    {
        var tasks = new List<Task>();
        if (InlinePlatformStats is null) tasks.Add(FetchPlatformStatsAsync(communityId));
        if (InlineTrendingSpaces is null) tasks.Add(FetchSpacesAsync(communityId));
        if (InlineTrendingContributors is null) tasks.Add(FetchContributorsAsync(communityId));
        await Task.WhenAll(tasks);
    }

    private async Task FetchPlatformStatsAsync(string? communityId)
    {
        try
        {
            if (!string.IsNullOrEmpty(communityId))
            {
                var data = await _apiClient.GetCommunityStatsAsync(communityId);
                if (data is not null) InlinePlatformStats = new(data.SpaceCount, data.DiscussionCount, data.ReplyCount, "eager");
            }
            else
            {
                var data = await _apiClient.GetPlatformStatsAsync();
                if (data is not null) InlinePlatformStats = new(data.SpaceCount, data.DiscussionCount, data.ReplyCount, "eager");
            }
        }
        catch (Exception ex) { logger.LogWarning(ex, "Failed to fetch platform stats"); }
    }

    private async Task FetchSpacesAsync(string? communityId)
    {
        try
        {
            var data = await _apiClient.GetTopActiveSpacesTodayAsync(communityId: communityId);
            if (data is not null) InlineTrendingSpaces = new(data, CommunityContext, "eager", "posts today");
        }
        catch (Exception ex) { logger.LogWarning(ex, "Failed to fetch active spaces"); }
    }

    private async Task FetchContributorsAsync(string? communityId)
    {
        try
        {
            var data = await _apiClient.GetTopContributorsTodayAsync(communityId: communityId);
            if (data is not null) InlineTrendingContributors = new(data, CommunityContext, "eager", "posts today");
        }
        catch (Exception ex) { logger.LogWarning(ex, "Failed to fetch active contributors"); }
    }

    private void ResolveSidebarData(string? communityId)
    {
        if (!string.IsNullOrEmpty(communityId))
        {
            var data = prefetchCache.ResolveOrPrefetch(
                $"platform-stats:community:{communityId}",
                () => _apiClient.GetCommunityStatsAsync(communityId));
            if (data is not null) InlinePlatformStats = new(data.SpaceCount, data.DiscussionCount, data.ReplyCount, "cache");
        }
        else
        {
            var data = prefetchCache.ResolveOrPrefetch(
                "platform-stats:platform:global",
                () => _apiClient.GetPlatformStatsAsync());
            if (data is not null) InlinePlatformStats = new(data.SpaceCount, data.DiscussionCount, data.ReplyCount, "cache");
        }

        InlineTrendingSpaces = prefetchCache.ResolveOrPrefetch(
            $"active-spaces:{SidebarScopeType}:{SidebarScopeId}",
            () => _apiClient.GetTopActiveSpacesTodayAsync(communityId: communityId),
            d => new SidebarTrendingSpacesVM(d, CommunityContext, "cache", "posts today"));

        InlineTrendingContributors = prefetchCache.ResolveOrPrefetch(
            $"active-contributors:{SidebarScopeType}:{SidebarScopeId}",
            () => _apiClient.GetTopContributorsTodayAsync(communityId: communityId),
            d => new SidebarTrendingContributorsVM(d, CommunityContext, "cache", "posts today"));
    }
}
