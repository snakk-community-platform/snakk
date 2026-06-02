using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Snakk.Web.Helpers;
using Snakk.Web.Pages.ViewModels;
using Snakk.Web.Services;
using Snakk.Protos.Community;
using Snakk.Protos.Discussion;
using Snakk.Protos.Hub;
using Snakk.Protos.Space;

namespace Snakk.Web.Pages.Hubs;

[OutputCache(PolicyName = "AnonymousPage")]
public class DetailModel(
    SnakkApiClient apiClient,
    IConfiguration configuration,
    ICommunityContext communityContext,
    IPrefetchCacheService prefetchCache) : BasePageModel(configuration, communityContext)
{
    private readonly SnakkApiClient _apiClient = apiClient;

    public HubInfo? Hub { get; set; }
    public HubStats? HubStats { get; set; }
    public CommunityInfo? CommunityDetail { get; set; }
    public PagedSpaceByHubList? Spaces { get; set; }
    public PagedRecentDiscussionList? RecentDiscussions { get; set; }
    public string Slug { get; set; } = string.Empty;
    public Dictionary<string, string> SpaceSparklineJson { get; set; } = new();

    // Sidebar scope for HTMX partials
    public string SidebarScopeType { get; set; } = "hub";
    public string SidebarScopeId { get; set; } = string.Empty;

    // Trending settings
    public bool ShowTrendingDiscussions => Configuration.GetValue("Trending:SpaceList:ShowDiscussions", true);
    public bool ShowTrendingContributors => Configuration.GetValue("Trending:SpaceList:ShowContributors", true);

    // Banners (bubble-down: hub + community)
    public Snakk.Protos.Banner.BannerList? Banners { get; set; }

    // 2FA gating: true when the hub requires 2FA and the visitor doesn't have it enabled.
    public bool Require2FAGate { get; set; }

    // Inline sidebar data (populated from cache, null = HTMX fallback)
    public SidebarTrendingDiscussionsVM? InlineTrendingDiscussions { get; set; }
    public SidebarTrendingContributorsVM? InlineTrendingContributors { get; set; }
    public SidebarHubRulesVM? InlineHubRules { get; set; }
    public SidebarModeratorsVM? InlineModerators { get; set; }

    public async Task<IActionResult> OnGetAsync(string slug, int offset = 0, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Slug = slug;

        var hubResult = await _apiClient.GetHubBySlugResultAsync(slug, CommunityContext.CommunitySlug!);
        if (!hubResult.IsSuccess)
            return hubResult.Status == GrpcStatus.NotFound ? NotFound() : StatusCode(503);

        Hub = hubResult.Value!;
        SidebarScopeId = Hub.PublicId;

        var viewerAllowsAdult = await AdultContentGate.ViewerAllowsAdultAsync(HttpContext, _apiClient);

        // Check cache for sidebar data — inline if warm, prefetch if cold
        ResolveSidebarData(viewerAllowsAdult);
        var spacesTask = _apiClient.GetSpacesByHubAsync(Hub.PublicId, 0, 50);
        var discussionsTask = _apiClient.GetRecentDiscussionsAsync(offset, 20, hubId: Hub.PublicId, viewerAllowsAdult: viewerAllowsAdult);
        var communityTask = !string.IsNullOrEmpty(CommunityContext.CommunitySlug)
            ? _apiClient.GetCommunityBySlugAsync(CommunityContext.CommunitySlug)
            : Task.FromResult<CommunityInfo?>(null);
        var announcementsTask = _apiClient.GetActiveBannersForHubAsync(Hub.PublicId);
        var statsTask = _apiClient.GetHubStatsAsync(Hub.PublicId);

        await Task.WhenAll(spacesTask, discussionsTask, communityTask, announcementsTask, statsTask);

        Spaces = spacesTask.IsCompletedSuccessfully ? spacesTask.Result : null;
        RecentDiscussions = discussionsTask.IsCompletedSuccessfully ? discussionsTask.Result : null;

        if (Spaces?.Items is { Count: > 0 } spaceItems)
        {
            var spaceIds = spaceItems.Select(s => s.PublicId).ToList();
            var batchResult = await _apiClient.GetActivitySparklinesBatchAsync(spaceIds, 7);
            if (batchResult != null)
            {
                foreach (var entry in batchResult.Entries)
                {
                    if (entry.Days.Count > 0)
                        SpaceSparklineJson[entry.PublicId] = System.Text.Json.JsonSerializer.Serialize(
                            entry.Days.Select(d => new { date = d.Date, postCount = d.PostCount, discussionCount = d.DiscussionCount }));
                }
            }
        }
        CommunityDetail = communityTask.IsCompletedSuccessfully ? communityTask.Result : null;
        Banners = announcementsTask.IsCompletedSuccessfully ? announcementsTask.Result : null;
        HubStats = statsTask.IsCompletedSuccessfully ? statsTask.Result : null;

        // Group access check — only call if hub or its community is restricted
        if (CommunityDetail is not null && (Hub.IsRestricted || CommunityDetail.IsRestricted))
        {
            var access = await _apiClient.CheckGroupAccessAsync(
                CommunityDetail.PublicId,
                Hub.PublicId);

            if (access is not null && access.AccessLevel < 1)
                return StatusCode(403);
        }

        // 2FA gate — short-circuit if the hub requires 2FA and the visitor doesn't have it
        if (Hub.Require2Fa && User.FindFirst("tfa")?.Value != "1")
        {
            Require2FAGate = true;
            return Page();
        }

        return Page();
    }

    private void ResolveSidebarData(bool viewerAllowsAdult)
    {
        var adultSuffix = viewerAllowsAdult ? "adult" : "safe";
        if (ShowTrendingDiscussions)
            InlineTrendingDiscussions = prefetchCache.ResolveOrPrefetch(
                $"trending-discussions:{SidebarScopeType}:{SidebarScopeId}:{adultSuffix}",
                () => _apiClient.GetTopActiveDiscussionsTodayAsync(Hub!.PublicId, viewerAllowsAdult: viewerAllowsAdult),
                d => new SidebarTrendingDiscussionsVM(d, CommunityContext, "cache"));

        if (ShowTrendingContributors)
            InlineTrendingContributors = prefetchCache.ResolveOrPrefetch(
                $"trending-contributors:{SidebarScopeType}:{SidebarScopeId}",
                () => _apiClient.GetTopContributorsTodayAsync(Hub!.PublicId),
                d => new SidebarTrendingContributorsVM(d, CommunityContext, "cache"));

        if (Hub!.HasRules)
            InlineHubRules = prefetchCache.ResolveOrPrefetch(
                $"hub-rules:{SidebarScopeId}",
                () => _apiClient.GetHubRulesAsync(SidebarScopeId),
                d => new SidebarHubRulesVM(
                    d,
                    CommunityContext,
                    CommunityContext.CommunitySlug ?? "",
                    Hub.ParentCommunityHasRules,
                    "cache"));

        InlineModerators = prefetchCache.ResolveOrPrefetch(
            $"moderators:Hub:{SidebarScopeId}",
            () => _apiClient.GetModeratorsAsync("Hub", SidebarScopeId),
            d => new SidebarModeratorsVM(
                d,
                $"{Helpers.SnakkUrlHelper.Hub(CommunityContext, Slug)}/moderators",
                "cache"));
    }
}
