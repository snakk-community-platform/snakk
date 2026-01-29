using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.OutputCaching;
using Snakk.Web.Models;
using Snakk.Web.Services;

namespace Snakk.Web.Pages.Spaces;

[OutputCache(PolicyName = "Space")]
public class DetailModel(SnakkApiClient apiClient, IConfiguration configuration, ICommunityContext communityContext) : BasePageModel(configuration, communityContext)
{
    private readonly SnakkApiClient _apiClient = apiClient;

    public SpaceDetailDto? Space { get; set; }
    public HubDetailDto? Hub { get; set; }
    public PagedResult<DiscussionDto>? Discussions { get; set; }
    public TopActiveDiscussionsResult? TrendingDiscussions { get; set; }
    public TopContributorsResult? TrendingContributors { get; set; }
    public SpaceStatsDto? SpaceStats { get; set; }
    public string HubSlug { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public bool IsAuthenticated { get; set; }
    public bool PreferEndlessScroll { get; set; } = true;

    // Trending settings
    public bool ShowTrendingDiscussions => Configuration.GetValue("Trending:DiscussionList:ShowDiscussions", true);
    public bool ShowTrendingContributors => Configuration.GetValue("Trending:DiscussionList:ShowContributors", true);

    public async Task<IActionResult> OnGetAsync(string hubSlug, string slug, int offset = 0)
    {
        HubSlug = hubSlug;
        Slug = slug;

        // Fetch hub, space, and user info in parallel
        var hubTask = _apiClient.GetHubBySlugAsync(hubSlug);
        var spaceTask = _apiClient.GetSpaceBySlugAsync(slug);
        var userTask = _apiClient.GetCurrentUserAsync();

        await Task.WhenAll(hubTask, spaceTask, userTask);

        Hub = hubTask.Result;
        Space = spaceTask.Result;

        // Set authentication status and preferences
        var user = userTask.IsCompletedSuccessfully ? userTask.Result : null;
        IsAuthenticated = user != null;
        PreferEndlessScroll = user?.PreferEndlessScroll ?? true;

        if (Space == null)
            return NotFound();

        // Fetch discussions, stats, and trending data in parallel
        var discussionsTask = _apiClient.GetDiscussionsBySpaceAsync(Space.PublicId, offset, 20);
        var statsTask = _apiClient.GetSpaceStatsAsync(Space.PublicId);

        Task<TopActiveDiscussionsResult?>? trendingTask = null;
        Task<TopContributorsResult?>? contributorsTask = null;

        var tasks = new List<Task> { discussionsTask, statsTask };

        if (ShowTrendingDiscussions)
        {
            trendingTask = _apiClient.GetTopActiveDiscussionsTodayAsync(spaceId: Space.PublicId);
            tasks.Add(trendingTask);
        }
        if (ShowTrendingContributors)
        {
            contributorsTask = _apiClient.GetTopContributorsTodayAsync(spaceId: Space.PublicId);
            tasks.Add(contributorsTask);
        }

        await Task.WhenAll(tasks);

        Discussions = discussionsTask.IsCompletedSuccessfully ? discussionsTask.Result : null;
        SpaceStats = statsTask.IsCompletedSuccessfully ? statsTask.Result : null;
        TrendingDiscussions = trendingTask?.IsCompletedSuccessfully == true ? trendingTask.Result : null;
        TrendingContributors = contributorsTask?.IsCompletedSuccessfully == true ? contributorsTask.Result : null;

        return Page();
    }
}
