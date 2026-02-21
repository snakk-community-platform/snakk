using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.OutputCaching;
using Snakk.Web.Services;
using Snakk.Web.Models;

namespace Snakk.Web.Pages.Hubs;

[OutputCache(PolicyName = "HomePage")]
public class IndexModel(SnakkApiClient apiClient, IConfiguration configuration, ICommunityContext communityContext) : PageModel
{
    private readonly SnakkApiClient _apiClient = apiClient;

    public PagedResult<HubDto>? Hubs { get; set; }
    public PlatformStatsDto? PlatformStats { get; set; }
    public CommunityStatsDto? CommunityStats { get; set; }
    public TopActiveSpacesResult? TrendingSpaces { get; set; }
    public TopActiveDiscussionsResult? TrendingDiscussions { get; set; }
    public TopContributorsResult? TrendingContributors { get; set; }
    public string ApiBaseUrl => configuration["ApiBaseUrl"] ?? "https://localhost:7291";
    public ICommunityContext Community => communityContext;

    // Stats accessor that works for both platform and community-scoped views
    public int HubCount => CommunityStats?.HubCount ?? PlatformStats?.HubCount ?? 0;
    public int SpaceCount => CommunityStats?.SpaceCount ?? PlatformStats?.SpaceCount ?? 0;
    public int DiscussionCount => CommunityStats?.DiscussionCount ?? PlatformStats?.DiscussionCount ?? 0;
    public int ReplyCount => CommunityStats?.ReplyCount ?? PlatformStats?.ReplyCount ?? 0;

    // Trending settings
    public bool ShowTrendingDiscussions => configuration.GetValue("Trending:HubList:ShowDiscussions", true);
    public bool ShowTrendingSpaces => configuration.GetValue("Trending:HubList:ShowSpaces", true);
    public bool ShowTrendingContributors => configuration.GetValue("Trending:HubList:ShowContributors", true);

    // Whether to show community in breadcrumb (multi-community enabled, default community, not on custom domain)
    public bool ShowCommunityInBreadcrumb =>
        configuration.GetValue<bool>("Features:MultiCommunityEnabled") &&
        communityContext.IsDefaultCommunity &&
        !communityContext.IsCustomDomain;

    public async Task OnGetAsync(int offset = 0)
    {
        // Determine if we need to scope to a community
        string? communityId = null;
        if (communityContext.IsCustomDomain && !string.IsNullOrEmpty(communityContext.CommunitySlug))
        {
            // Get the community to retrieve its public ID
            var community = await _apiClient.GetCommunityBySlugAsync(communityContext.CommunitySlug);
            communityId = community?.PublicId;
        }

        // Use community-scoped hub list if on custom domain, otherwise all hubs
        Task<PagedResult<HubDto>?> hubsTask;
        if (!string.IsNullOrEmpty(communityId))
        {
            hubsTask = _apiClient.GetHubsByCommunityAsync(communityId, offset, 20);
        }
        else
        {
            hubsTask = _apiClient.GetHubsAsync(offset, 20);
        }

        var tasks = new List<Task> { hubsTask };

        // Use community stats if on custom domain, otherwise platform stats
        Task<CommunityStatsDto?>? communityStatsTask = null;
        Task<PlatformStatsDto?>? platformStatsTask = null;

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

        Task<TopActiveSpacesResult?>? spacesTask = null;
        Task<TopActiveDiscussionsResult?>? discussionsTask = null;
        Task<TopContributorsResult?>? contributorsTask = null;

        if (ShowTrendingSpaces)
        {
            spacesTask = _apiClient.GetTopActiveSpacesTodayAsync(communityId: communityId);
            tasks.Add(spacesTask);
        }
        if (ShowTrendingDiscussions)
        {
            discussionsTask = _apiClient.GetTopActiveDiscussionsTodayAsync(communityId: communityId);
            tasks.Add(discussionsTask);
        }
        if (ShowTrendingContributors)
        {
            contributorsTask = _apiClient.GetTopContributorsTodayAsync(communityId: communityId);
            tasks.Add(contributorsTask);
        }

        await Task.WhenAll(tasks);

        Hubs = hubsTask.IsCompletedSuccessfully ? hubsTask.Result : null;
        PlatformStats = platformStatsTask?.IsCompletedSuccessfully == true ? platformStatsTask.Result : null;
        CommunityStats = communityStatsTask?.IsCompletedSuccessfully == true ? communityStatsTask.Result : null;
        TrendingSpaces = spacesTask?.IsCompletedSuccessfully == true ? spacesTask.Result : null;
        TrendingDiscussions = discussionsTask?.IsCompletedSuccessfully == true ? discussionsTask.Result : null;
        TrendingContributors = contributorsTask?.IsCompletedSuccessfully == true ? contributorsTask.Result : null;
    }
}
