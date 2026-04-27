using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.OutputCaching;
using Snakk.Web.Helpers;
using Snakk.Web.Pages.ViewModels;
using Snakk.Web.Services;
using Snakk.Protos.Community;
using Snakk.Protos.Hub;
using Snakk.Protos.Space;
using Snakk.Protos.Discussion;

namespace Snakk.Web.Pages.Spaces;

[OutputCache(PolicyName = "AnonymousPage")]
public class DetailModel(
    SnakkApiClient apiClient,
    IConfiguration configuration,
    ICommunityContext communityContext,
    IPrefetchCacheService prefetchCache) : BasePageModel(configuration, communityContext)
{
    private readonly SnakkApiClient _apiClient = apiClient;

    public SpaceInfo? Space { get; set; }
    public HubInfo? Hub { get; set; }
    public CommunityInfo? CommunityDetail { get; set; }
    public Snakk.Protos.Discussion.PagedRecentDiscussionList? Discussions { get; set; }
    public SpaceStats? SpaceStats { get; set; }
    public string HubSlug { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public bool IsAuthenticated { get; set; }

    // Sidebar scope for HTMX partials
    public string SidebarScopeType { get; set; } = "space";
    public string SidebarScopeId { get; set; } = string.Empty;

    // Adult-content gating: when the space is adult-only and the visitor hasn't
    // confirmed/declined, the page renders an interstitial instead of content.
    public AdultContentState AdultGateState { get; set; } = AdultContentState.Allowed;

    // Trending settings
    public bool ShowTrendingDiscussions => Configuration.GetValue("Trending:DiscussionList:ShowDiscussions", true);
    public bool ShowTrendingContributors => Configuration.GetValue("Trending:DiscussionList:ShowContributors", true);

    // Banners (bubble-down: space + hub + community)
    public Snakk.Protos.Banner.BannerList? Banners { get; set; }

    // Inline sidebar data (populated from cache, null = HTMX fallback)
    public SidebarTrendingDiscussionsVM? InlineTrendingDiscussions { get; set; }
    public SidebarTrendingContributorsVM? InlineTrendingContributors { get; set; }
    public SidebarSpaceRulesVM? InlineSpaceRules { get; set; }
    public SidebarModeratorsVM? InlineModerators { get; set; }

    public async Task<IActionResult> OnGetAsync(string hubSlug, string slug, int offset = 0)
    {
        HubSlug = hubSlug;
        Slug = slug;

        // Read preferences from cookies (no API call needed)
        IsAuthenticated = HttpContext.Request.Cookies.ContainsKey(AuthCookieHelper.AccessCookieName);

        // Fetch hub, space, and community in parallel
        var hubTask = _apiClient.GetHubBySlugResultAsync(hubSlug, CommunityContext.CommunitySlug!);
        var spaceTask = _apiClient.GetSpaceBySlugResultAsync(slug, hubSlug);
        var communityTask = !string.IsNullOrEmpty(CommunityContext.CommunitySlug)
            ? _apiClient.GetCommunityBySlugAsync(CommunityContext.CommunitySlug)
            : Task.FromResult<CommunityInfo?>(null);

        await Task.WhenAll(hubTask, spaceTask, communityTask);

        Hub = hubTask.Result.IsSuccess ? hubTask.Result.Value : null;
        CommunityDetail = communityTask.IsCompletedSuccessfully ? communityTask.Result : null;

        if (!spaceTask.Result.IsSuccess)
            return spaceTask.Result.Status == GrpcStatus.NotFound ? NotFound() : StatusCode(503);

        Space = spaceTask.Result.Value!;

        // Adult-content gating — short-circuit before loading anything else
        if (Space.IsAdultOnly)
        {
            bool? userPref = null;
            if (IsAuthenticated)
            {
                var user = await _apiClient.GetCurrentUserAsync();
                userPref = user?.HasAllowAdultContent == true ? user.AllowAdultContent : null;
            }

            AdultGateState = AdultContentGate.GetState(HttpContext, userPref, contentIsAdult: true);
            if (AdultGateState != AdultContentState.Allowed)
                return Page();
        }

        // Group access check — only call if any level in the hierarchy is restricted
        if (CommunityDetail is not null
            && (Space.IsRestricted || Hub?.IsRestricted == true || CommunityDetail.IsRestricted))
        {
            var access = await _apiClient.CheckGroupAccessAsync(
                CommunityDetail.PublicId,
                Hub?.PublicId,
                Space.PublicId);

            if (access is not null && access.AccessLevel < 1)
                return StatusCode(403);
        }

        SidebarScopeId = Space.PublicId;

        var viewerAllowsAdult = await AdultContentGate.ViewerAllowsAdultAsync(HttpContext, _apiClient);

        // Check cache for sidebar data — inline if warm, prefetch if cold
        ResolveSidebarData(viewerAllowsAdult);

        // Fetch discussions, stats, and announcements in parallel
        var discussionsTask = _apiClient.GetRecentDiscussionsAsync(spaceId: Space.PublicId, pageSize: 20, viewerAllowsAdult: viewerAllowsAdult);
        var statsTask = _apiClient.GetSpaceStatsAsync(Space.PublicId);
        var announcementsTask = _apiClient.GetActiveBannersForSpaceAsync(Space.PublicId);

        await Task.WhenAll(discussionsTask, statsTask, announcementsTask);

        Discussions = discussionsTask.IsCompletedSuccessfully ? discussionsTask.Result : null;
        SpaceStats = statsTask.IsCompletedSuccessfully ? statsTask.Result : null;
        Banners = announcementsTask.IsCompletedSuccessfully ? announcementsTask.Result : null;

        return Page();
    }

    private void ResolveSidebarData(bool viewerAllowsAdult)
    {
        var adultSuffix = viewerAllowsAdult ? "adult" : "safe";
        if (ShowTrendingDiscussions)
            InlineTrendingDiscussions = prefetchCache.ResolveOrPrefetch(
                $"trending-discussions:{SidebarScopeType}:{SidebarScopeId}:{adultSuffix}",
                () => _apiClient.GetTopActiveDiscussionsTodayAsync(spaceId: SidebarScopeId, viewerAllowsAdult: viewerAllowsAdult),
                d => new SidebarTrendingDiscussionsVM(d, CommunityContext, "cache"));

        if (ShowTrendingContributors)
            InlineTrendingContributors = prefetchCache.ResolveOrPrefetch(
                $"trending-contributors:{SidebarScopeType}:{SidebarScopeId}",
                () => _apiClient.GetTopContributorsTodayAsync(spaceId: SidebarScopeId),
                d => new SidebarTrendingContributorsVM(d, CommunityContext, "cache"));

        InlineSpaceRules = prefetchCache.ResolveOrPrefetch(
            $"space-rules:{SidebarScopeId}",
            () => _apiClient.GetSpaceRulesAsync(SidebarScopeId),
            d => new SidebarSpaceRulesVM(
                d,
                CommunityContext,
                HubSlug,
                CommunityContext.CommunitySlug ?? "",
                Space?.ParentHubHasRules ?? false,
                Space?.ParentCommunityHasRules ?? false,
                "cache"));

        InlineModerators = prefetchCache.ResolveOrPrefetch(
            $"moderators:Space:{SidebarScopeId}",
            () => _apiClient.GetModeratorsAsync("Space", SidebarScopeId),
            d => new SidebarModeratorsVM(
                d,
                $"{Helpers.SnakkUrlHelper.Space(CommunityContext, HubSlug, Slug)}/moderators",
                "cache"));
    }
}
