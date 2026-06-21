using Microsoft.AspNetCore.Mvc;
using Snakk.Web.Helpers;
using Snakk.Web.Pages.ViewModels;
using Snakk.Web.Services;

namespace Snakk.Web.ViewComponents;

public class GlobalRightSidebarViewComponent(
    SnakkApiClient apiClient,
    IPrefetchCacheService prefetchCache,
    ICommunityContext communityContext) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        if (DeviceDetection.IsMobile(HttpContext))
            return Content(string.Empty);
        var statsTask = prefetchCache.GetOrFetchAsync(
            "platform-stats:platform:global",
            () => apiClient.GetPlatformStatsAsync());

        var spacesTask = prefetchCache.GetOrFetchAsync(
            "active-spaces:platform:global",
            () => apiClient.GetTopActiveSpacesTodayAsync());

        var contributorsTask = prefetchCache.GetOrFetchAsync(
            "active-contributors:platform:global",
            () => apiClient.GetTopContributorsTodayAsync());

        var statsResult = await statsTask;
        var spacesResult = await spacesTask;
        var contributorsResult = await contributorsTask;

        SidebarPlatformStatsVM? stats = null;
        if (statsResult.Value is { } s)
            stats = new SidebarPlatformStatsVM(s.SpaceCount, s.DiscussionCount, s.ReplyCount, statsResult.Source);

        var spaces = spacesResult.Value is not null
            ? new SidebarTrendingSpacesVM(spacesResult.Value, communityContext, spacesResult.Source)
            : null;

        var contributors = contributorsResult.Value is not null
            ? new SidebarTrendingContributorsVM(contributorsResult.Value, communityContext, contributorsResult.Source)
            : null;

        return View(new GlobalRightSidebarVM(stats, spaces, contributors));
    }
}
