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
    public int Offset { get; private set; }

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
        "all-time" => "All Time",
        _          => "Last Week"
    };

    private string SpacesLabel => Period switch
    {
        "day"      => "posts today",
        "month"    => "posts this month",
        "year"     => "posts this year",
        "all-time" => "all-time posts",
        _          => "posts this week"
    };

    private static string ToApiPeriod(string period) => period == "all-time" ? "all_time" : period;

    public SidebarPlatformStatsVM? InlinePlatformStats { get; set; }
    public SidebarTrendingSpacesVM? InlineTrendingSpaces { get; set; }
    public SidebarTrendingContributorsVM? InlineTrendingContributors { get; set; }

    public async Task<IActionResult> OnGetAsync(string? period, [FromQuery] int offset = 0, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Request.Query.TryGetValue("period", out var queryPeriod) && !string.IsNullOrEmpty(queryPeriod))
        {
            var mapped = queryPeriod.ToString() == "all_time" ? "all-time" : queryPeriod.ToString();
            return RedirectToPage(new { period = mapped });
        }
        Period = period is "day" or "week" or "month" or "year" or "all-time"
            ? period
            : "week";
        Offset = offset;

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
        try { TopDiscussions = await _apiClient.GetTopDiscussionsAsync(offset, 20, communityId, ToApiPeriod(Period), viewerAllowsAdult: viewerAllowsAdult); }
        catch (Exception ex) { logger.LogWarning(ex, "Failed to fetch top discussions"); }
        return Page();
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
                var result = await prefetchCache.GetOrFetchAsync(
                    $"platform-stats:community:{communityId}",
                    () => _apiClient.GetCommunityStatsAsync(communityId),
                    sharedTtl: TimeSpan.FromSeconds(30));
                if (result.Value is not null)
                    InlinePlatformStats = new(result.Value.SpaceCount, result.Value.DiscussionCount, result.Value.ReplyCount, result.Source);
            }
            else
            {
                var result = await prefetchCache.GetOrFetchAsync(
                    "platform-stats:platform:global",
                    () => _apiClient.GetPlatformStatsAsync(),
                    sharedTtl: TimeSpan.FromMinutes(5));
                if (result.Value is not null)
                    InlinePlatformStats = new(result.Value.SpaceCount, result.Value.DiscussionCount, result.Value.ReplyCount, result.Source);
            }
        }
        catch (Exception ex) { logger.LogWarning(ex, "Failed to fetch platform stats"); }
    }

    private async Task FetchSpacesAsync(string? communityId)
    {
        try
        {
            var result = await prefetchCache.GetOrFetchAsync(
                $"top-spaces:{Period}:{SidebarScopeType}:{SidebarScopeId}",
                () => _apiClient.GetTopSpacesByPeriodAsync(ToApiPeriod(Period), communityId: communityId));
            if (result.Value is not null)
                InlineTrendingSpaces = new(result.Value, CommunityContext, result.Source, SpacesLabel);
        }
        catch (Exception ex) { logger.LogWarning(ex, "Failed to fetch top spaces"); }
    }

    private async Task FetchContributorsAsync(string? communityId)
    {
        try
        {
            var result = await prefetchCache.GetOrFetchAsync(
                $"top-contributors:{Period}:{SidebarScopeType}:{SidebarScopeId}",
                () => _apiClient.GetTopContributorsByPeriodAsync(ToApiPeriod(Period), communityId: communityId));
            if (result.Value is not null)
                InlineTrendingContributors = new(result.Value, CommunityContext, result.Source, SpacesLabel);
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
            () => _apiClient.GetTopSpacesByPeriodAsync(ToApiPeriod(Period), communityId: communityId),
            d => new SidebarTrendingSpacesVM(d, CommunityContext, "cache", SpacesLabel));

        InlineTrendingContributors = prefetchCache.ResolveOrPrefetch(
            $"top-contributors:{Period}:{SidebarScopeType}:{SidebarScopeId}",
            () => _apiClient.GetTopContributorsByPeriodAsync(ToApiPeriod(Period), communityId: communityId),
            d => new SidebarTrendingContributorsVM(d, CommunityContext, "cache", SpacesLabel));
    }
}
