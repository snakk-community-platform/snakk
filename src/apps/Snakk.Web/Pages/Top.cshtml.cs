using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Snakk.Protos.Discussion;
using Snakk.Web.Pages.ViewModels;
using Snakk.Web.Services;

namespace Snakk.Web.Pages;

public class TopModel(
    SnakkApiClient apiClient,
    IConfiguration configuration,
    ICommunityContext communityContext,
    IPrefetchCacheService prefetchCache,
    ILogger<TopModel> logger) : BasePageModel(configuration, communityContext)
{
    private readonly SnakkApiClient _apiClient = apiClient;

    public PagedRecentDiscussionList? TopDiscussions { get; set; }
    public string Period { get; set; } = "week";

    public string SidebarScopeType { get; set; } = "platform";
    public string SidebarScopeId { get; set; } = "global";

    public bool ShowCommunityInDiscussionList =>
        CommunityContext.IsMultiCommunityEnabled
        && string.IsNullOrEmpty(CommunityContext.CommunitySlug)
        && !CommunityContext.IsCustomDomain;

    public string PeriodLabel => Period switch
    {
        "day"      => "Today",
        "week"     => "Last Week",
        "month"    => "Last Month",
        "year"     => "Last Year",
        "all_time" => "All Time",
        _          => "Last Week"
    };

    private string SpacesLabel => Period switch
    {
        "day"      => "posts today",
        "month"    => "posts this month",
        "year"     => "posts this year",
        "all_time" => "all-time posts",
        _          => "posts this week"
    };

    public SidebarPlatformStatsVM? InlinePlatformStats { get; set; }
    public SidebarTrendingSpacesVM? InlineTrendingSpaces { get; set; }
    public SidebarTrendingContributorsVM? InlineTrendingContributors { get; set; }

    public async Task OnGetAsync([FromQuery] string period = "week", [FromQuery] int offset = 0)
    {
        Period = period is "day" or "week" or "month" or "year" or "all_time"
            ? period
            : "week";

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

        var viewerAllowsAdult = await AdultContentGate.ViewerAllowsAdultAsync(HttpContext, _apiClient);
        try { TopDiscussions = await _apiClient.GetTopDiscussionsAsync(offset, 20, communityId, Period, viewerAllowsAdult: viewerAllowsAdult); }
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
            var data = await _apiClient.GetTopSpacesByPeriodAsync(Period, communityId: communityId);
            if (data is not null) InlineTrendingSpaces = new(data, CommunityContext, "eager", SpacesLabel);
        }
        catch (Exception ex) { logger.LogWarning(ex, "Failed to fetch top spaces"); }
    }

    private async Task FetchContributorsAsync(string? communityId)
    {
        try
        {
            var data = await _apiClient.GetTopContributorsByPeriodAsync(Period, communityId: communityId);
            if (data is not null) InlineTrendingContributors = new(data, CommunityContext, "eager", SpacesLabel);
        }
        catch (Exception ex) { logger.LogWarning(ex, "Failed to fetch top contributors"); }
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
            $"top-spaces:{Period}:{SidebarScopeType}:{SidebarScopeId}",
            () => _apiClient.GetTopSpacesByPeriodAsync(Period, communityId: communityId),
            d => new SidebarTrendingSpacesVM(d, CommunityContext, "cache", SpacesLabel));

        InlineTrendingContributors = prefetchCache.ResolveOrPrefetch(
            $"top-contributors:{Period}:{SidebarScopeType}:{SidebarScopeId}",
            () => _apiClient.GetTopContributorsByPeriodAsync(Period, communityId: communityId),
            d => new SidebarTrendingContributorsVM(d, CommunityContext, "cache", SpacesLabel));
    }
}
