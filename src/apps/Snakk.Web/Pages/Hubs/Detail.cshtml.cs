using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.OutputCaching;
using Snakk.Web.Services;
using Snakk.Web.Models;

namespace Snakk.Web.Pages.Hubs;

[OutputCache(PolicyName = "Space")]
public class DetailModel(SnakkApiClient apiClient, IConfiguration configuration, ICommunityContext communityContext) : PageModel
{
    private readonly SnakkApiClient _apiClient = apiClient;

    public HubDetailDto? Hub { get; set; }
    public PagedResult<SpaceDto>? Spaces { get; set; }
    public TopActiveDiscussionsResult? TrendingDiscussions { get; set; }
    public TopContributorsResult? TrendingContributors { get; set; }
    public HubStatsDto? HubStats { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string ApiBaseUrl => configuration["ApiBaseUrl"] ?? "https://localhost:7291";
    public ICommunityContext Community => communityContext;

    // Trending settings
    public bool ShowTrendingDiscussions => configuration.GetValue("Trending:SpaceList:ShowDiscussions", true);
    public bool ShowTrendingContributors => configuration.GetValue("Trending:SpaceList:ShowContributors", true);

    // Whether to show community in breadcrumb (multi-community enabled, default community, not on custom domain)
    public bool ShowCommunityInBreadcrumb =>
        configuration.GetValue<bool>("Features:MultiCommunityEnabled") &&
        communityContext.IsDefaultCommunity &&
        !communityContext.IsCustomDomain;

    public async Task<IActionResult> OnGetAsync(string slug, int offset = 0)
    {
        Slug = slug;

        Hub = await _apiClient.GetHubBySlugAsync(slug);
        if (Hub == null)
            return NotFound();

        var spacesTask = _apiClient.GetSpacesByHubAsync(Hub.PublicId, offset, 20);
        var statsTask = _apiClient.GetHubStatsAsync(Hub.PublicId);

        Task<TopActiveDiscussionsResult?>? trendingTask = null;
        Task<TopContributorsResult?>? contributorsTask = null;

        var tasks = new List<Task> { spacesTask, statsTask };

        if (ShowTrendingDiscussions)
        {
            trendingTask = _apiClient.GetTopActiveDiscussionsTodayAsync(Hub.PublicId);
            tasks.Add(trendingTask);
        }
        if (ShowTrendingContributors)
        {
            contributorsTask = _apiClient.GetTopContributorsTodayAsync(Hub.PublicId);
            tasks.Add(contributorsTask);
        }

        await Task.WhenAll(tasks);

        Spaces = spacesTask.IsCompletedSuccessfully ? spacesTask.Result : null;
        HubStats = statsTask.IsCompletedSuccessfully ? statsTask.Result : null;
        TrendingDiscussions = trendingTask?.IsCompletedSuccessfully == true ? trendingTask.Result : null;
        TrendingContributors = contributorsTask?.IsCompletedSuccessfully == true ? contributorsTask.Result : null;

        return Page();
    }
}
