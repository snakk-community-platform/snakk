namespace Snakk.Web.Pages.My;

using System.Security.Claims;
using Snakk.Protos.Discussion;
using Snakk.Web.Pages.ViewModels;
using Snakk.Web.Services;

public class FeedModel(
    SnakkApiClient apiClient,
    IConfiguration configuration,
    ICommunityContext communityContext,
    IPrefetchCacheService prefetchCache,
    IFollowedSpacesCacheService followedSpacesCache,
    ILogger<FeedModel> logger) : BasePageModel(configuration, communityContext)
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

    public async Task OnGetAsync(string? cursor = null, CancellationToken cancellationToken = default)
    {
        Preload("discussion-card");
        cancellationToken.ThrowIfCancellationRequested();
        var statsData = prefetchCache.ResolveOrPrefetch(
            "platform-stats:platform:global",
            () => apiClient.GetPlatformStatsAsync());
        if (statsData is not null)
            InlinePlatformStats = new(statsData.SpaceCount, statsData.DiscussionCount, statsData.ReplyCount, "cache");

        IsAuthenticated = HttpContext.Request.Cookies.ContainsKey(AuthCookieHelper.AccessCookieName);
        if (!IsAuthenticated) return;

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.NameId)?.Value;
        if (userId is null) return;

        var spaceIds = await followedSpacesCache.GetAsync(userId, ct => apiClient.GetFollowedSpacesAsync(ct), cancellationToken);
        HasFollowedSpaces = spaceIds.Count > 0;
        if (!HasFollowedSpaces) return;

        var viewerAllowsAdult = await AdultContentGate.ViewerAllowsAdultAsync(HttpContext, apiClient);
        try
        {
            var result = await apiClient.GetRecentDiscussionsAsync(pageSize: 20, cursor: cursor, spaceIds: spaceIds, viewerAllowsAdult: viewerAllowsAdult);
            Discussions = result;
            NextCursor = result?.HasNextCursor == true ? result.NextCursor : null;
        }
        catch (Exception ex) { logger.LogWarning(ex, "Failed to load feed discussions"); }
    }
}
