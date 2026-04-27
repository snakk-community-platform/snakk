namespace Snakk.Web.Pages;

using System.Security.Claims;
using Snakk.Protos.Discussion;
using Snakk.Web.Pages.ViewModels;
using Snakk.Web.Services;

public class MyFeedModel(
    SnakkApiClient apiClient,
    IConfiguration configuration,
    ICommunityContext communityContext,
    IPrefetchCacheService prefetchCache,
    IFollowedSpacesCacheService followedSpacesCache) : BasePageModel(configuration, communityContext)
{
    public bool IsAuthenticated { get; set; }
    public bool HasFollowedSpaces { get; set; }
    public PagedRecentDiscussionList? Discussions { get; set; }
    public string? NextCursor { get; set; }
    public SidebarPlatformStatsVM? InlinePlatformStats { get; set; }

    public bool ShowCommunityInDiscussionList =>
        CommunityContext.IsMultiCommunityEnabled
        && string.IsNullOrEmpty(CommunityContext.CommunitySlug)
        && !CommunityContext.IsCustomDomain;

    public async Task OnGetAsync(string? cursor = null)
    {
        var statsData = prefetchCache.ResolveOrPrefetch(
            "platform-stats:platform:global",
            () => apiClient.GetPlatformStatsAsync());
        if (statsData is not null)
            InlinePlatformStats = new(statsData.SpaceCount, statsData.DiscussionCount, statsData.ReplyCount, "cache");

        IsAuthenticated = HttpContext.Request.Cookies.ContainsKey(AuthCookieHelper.AccessCookieName);
        if (!IsAuthenticated) return;

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId is null) return;

        var spaceIds = await followedSpacesCache.GetAsync(userId, apiClient.GetFollowedSpacesAsync);
        HasFollowedSpaces = spaceIds.Count > 0;
        if (!HasFollowedSpaces) return;

        var viewerAllowsAdult = await AdultContentGate.ViewerAllowsAdultAsync(HttpContext, apiClient);
        try
        {
            var result = await apiClient.GetRecentDiscussionsAsync(pageSize: 20, cursor: cursor, spaceIds: spaceIds, viewerAllowsAdult: viewerAllowsAdult);
            Discussions = result;
            NextCursor = result?.HasNextCursor == true ? result.NextCursor : null;
        }
        catch { }
    }
}
