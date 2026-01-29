using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.OutputCaching;
using Snakk.Web.Models;
using Snakk.Web.Services;

namespace Snakk.Web.Pages;

[OutputCache(PolicyName = "HomePage")]
public class IndexModel(SnakkApiClient apiClient, IConfiguration configuration, ICommunityContext communityContext) : BasePageModel(configuration, communityContext)
{
    private readonly SnakkApiClient _apiClient = apiClient;

    public PagedResult<RecentDiscussionDto>? RecentDiscussions { get; set; }
    public TopActiveDiscussionsResult? TopActiveDiscussions { get; set; }
    public TopActiveSpacesResult? TopActiveSpaces { get; set; }
    public TopContributorsResult? TopContributors { get; set; }
    public PlatformStatsDto? PlatformStats { get; set; }
    public CommunityStatsDto? CommunityStats { get; set; }
    public bool PreferEndlessScroll { get; set; } = true;

    // Stats accessor that works for both platform and community-scoped views
    public int HubCount => CommunityStats?.HubCount ?? PlatformStats?.HubCount ?? 0;
    public int SpaceCount => CommunityStats?.SpaceCount ?? PlatformStats?.SpaceCount ?? 0;
    public int DiscussionCount => CommunityStats?.DiscussionCount ?? PlatformStats?.DiscussionCount ?? 0;
    public int ReplyCount => CommunityStats?.ReplyCount ?? PlatformStats?.ReplyCount ?? 0;

    // Trending settings
    public bool ShowTrendingDiscussions => Configuration.GetValue("Trending:FrontPage:ShowDiscussions", true);
    public bool ShowTrendingSpaces => Configuration.GetValue("Trending:FrontPage:ShowSpaces", true);
    public bool ShowTrendingContributors => Configuration.GetValue("Trending:FrontPage:ShowContributors", true);

    // Whether to show community in discussion list (multi-community enabled, default community, not on custom domain)
    public bool ShowCommunityInDiscussionList =>
        Configuration.GetValue<bool>("Features:MultiCommunityEnabled") &&
        CommunityContext.IsDefaultCommunity &&
        !CommunityContext.IsCustomDomain;

    public async Task OnGetAsync(int offset = 0)
    {
        // Fetch user preference if authenticated
        var userTask = _apiClient.GetCurrentUserAsync();

        // Determine if we need to scope to a community
        string? communityId = null;
        if (CommunityContext.IsCustomDomain && !string.IsNullOrEmpty(CommunityContext.CommunitySlug))
        {
            // Get the community to retrieve its public ID
            var community = await _apiClient.GetCommunityBySlugAsync(CommunityContext.CommunitySlug);
            communityId = community?.PublicId;
        }

        var recentDiscussionsTask = _apiClient.GetRecentDiscussionsAsync(offset, 50, communityId);
        var tasks = new List<Task> { recentDiscussionsTask, userTask };

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

        // Only fetch trending data if enabled
        Task<TopActiveDiscussionsResult?>? topDiscussionsTask = null;
        Task<TopActiveSpacesResult?>? topSpacesTask = null;
        Task<TopContributorsResult?>? topContributorsTask = null;

        if (ShowTrendingDiscussions)
        {
            topDiscussionsTask = _apiClient.GetTopActiveDiscussionsTodayAsync(communityId: communityId);
            tasks.Add(topDiscussionsTask);
        }
        if (ShowTrendingSpaces)
        {
            topSpacesTask = _apiClient.GetTopActiveSpacesTodayAsync(communityId: communityId);
            tasks.Add(topSpacesTask);
        }
        if (ShowTrendingContributors)
        {
            topContributorsTask = _apiClient.GetTopContributorsTodayAsync(communityId: communityId);
            tasks.Add(topContributorsTask);
        }

        try
        {
            await Task.WhenAll(tasks);
        }
        catch
        {
            // Continue with whatever succeeded
        }

        RecentDiscussions = recentDiscussionsTask.IsCompletedSuccessfully ? recentDiscussionsTask.Result : null;
        PlatformStats = platformStatsTask?.IsCompletedSuccessfully == true ? platformStatsTask.Result : null;
        CommunityStats = communityStatsTask?.IsCompletedSuccessfully == true ? communityStatsTask.Result : null;
        TopActiveDiscussions = topDiscussionsTask?.IsCompletedSuccessfully == true ? topDiscussionsTask.Result : null;
        TopActiveSpaces = topSpacesTask?.IsCompletedSuccessfully == true ? topSpacesTask.Result : null;
        TopContributors = topContributorsTask?.IsCompletedSuccessfully == true ? topContributorsTask.Result : null;

        // Get user preference (defaults to true if not authenticated)
        var user = userTask.IsCompletedSuccessfully ? userTask.Result : null;
        PreferEndlessScroll = user?.PreferEndlessScroll ?? true;
    }
}
