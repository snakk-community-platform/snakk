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

[OutputCache(PolicyName = "Space")]
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
    public PagedDiscussionBySpaceList? Discussions { get; set; }
    public SpaceStats? SpaceStats { get; set; }
    public string HubSlug { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public bool IsAuthenticated { get; set; }
    public bool PreferEndlessScroll { get; set; } = true;
    public int? TypeFilter { get; set; }

    // Discussion creation form fields
    [BindProperty] public string? NewTitle { get; set; }
    [BindProperty] public string? NewContent { get; set; }
    [BindProperty] public int NewType { get; set; }
    public string? CreateError { get; set; }

    // Sidebar scope for HTMX partials
    public string SidebarScopeType { get; set; } = "space";
    public string SidebarScopeId { get; set; } = string.Empty;

    // Trending settings
    public bool ShowTrendingDiscussions => Configuration.GetValue("Trending:DiscussionList:ShowDiscussions", true);
    public bool ShowTrendingContributors => Configuration.GetValue("Trending:DiscussionList:ShowContributors", true);

    // Inline sidebar data (populated from cache, null = HTMX fallback)
    public SidebarTrendingDiscussionsVM? InlineTrendingDiscussions { get; set; }
    public SidebarTrendingContributorsVM? InlineTrendingContributors { get; set; }
    public SidebarSpaceRulesVM? InlineSpaceRules { get; set; }

    public async Task<IActionResult> OnGetAsync(string hubSlug, string slug, int offset = 0, int? typeFilter = null)
    {
        HubSlug = hubSlug;
        Slug = slug;
        TypeFilter = typeFilter;

        // Read preferences from cookies (no API call needed)
        IsAuthenticated = HttpContext.Request.Cookies.ContainsKey(AuthCookieHelper.AccessCookieName);
        PreferEndlessScroll = AuthCookieHelper.GetPreferEndlessScroll(HttpContext);

        // Fetch hub, space, and community in parallel
        var hubTask = _apiClient.GetHubBySlugResultAsync(hubSlug);
        var spaceTask = _apiClient.GetSpaceBySlugResultAsync(slug);
        var communityTask = !string.IsNullOrEmpty(CommunityContext.CommunitySlug)
            ? _apiClient.GetCommunityBySlugAsync(CommunityContext.CommunitySlug)
            : Task.FromResult<CommunityInfo?>(null);

        await Task.WhenAll(hubTask, spaceTask, communityTask);

        Hub = hubTask.Result.IsSuccess ? hubTask.Result.Value : null;
        CommunityDetail = communityTask.IsCompletedSuccessfully ? communityTask.Result : null;

        if (!spaceTask.Result.IsSuccess)
            return spaceTask.Result.Status == GrpcStatus.NotFound ? NotFound() : StatusCode(503);

        Space = spaceTask.Result.Value!;

        SidebarScopeId = Space.PublicId;

        // Check cache for sidebar data — inline if warm, prefetch if cold
        ResolveSidebarData();

        // Fetch discussions and stats in parallel
        var discussionsTask = _apiClient.GetDiscussionsBySpaceAsync(Space.PublicId, offset, 20, TypeFilter);
        var statsTask = _apiClient.GetSpaceStatsAsync(Space.PublicId);

        await Task.WhenAll(discussionsTask, statsTask);

        Discussions = discussionsTask.IsCompletedSuccessfully ? discussionsTask.Result : null;
        SpaceStats = statsTask.IsCompletedSuccessfully ? statsTask.Result : null;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string hubSlug, string slug)
    {
        HubSlug = hubSlug;
        Slug = slug;

        IsAuthenticated = HttpContext.Request.Cookies.ContainsKey(AuthCookieHelper.AccessCookieName);

        if (!IsAuthenticated)
            return RedirectToPage("/Auth/Login", new { returnUrl = $"/h/{hubSlug}/{slug}" });

        if (string.IsNullOrWhiteSpace(NewTitle) || string.IsNullOrWhiteSpace(NewContent))
        {
            CreateError = "Title and content are required.";
            return await ReloadPageData(hubSlug, slug);
        }

        // Look up space to get PublicId
        var spaceResult = await _apiClient.GetSpaceBySlugResultAsync(slug);
        if (!spaceResult.IsSuccess || spaceResult.Value is null)
            return NotFound();

        Space = spaceResult.Value;

        var result = await _apiClient.CreateDiscussionAsync(
            Space.PublicId,
            NewTitle.Trim(),
            NewContent,
            NewType);

        if (result is null)
        {
            CreateError = "Failed to create discussion. Please try again.";
            return await ReloadPageData(hubSlug, slug);
        }

        var discussionUrl = SnakkUrlHelper.Discussion(
            CommunityContext,
            hubSlug,
            slug,
            $"{result.Slug}~{result.PublicId}");

        return Redirect(discussionUrl);
    }

    private async Task<IActionResult> ReloadPageData(string hubSlug, string slug)
    {
        HubSlug = hubSlug;
        Slug = slug;
        PreferEndlessScroll = AuthCookieHelper.GetPreferEndlessScroll(HttpContext);

        var hubTask = _apiClient.GetHubBySlugResultAsync(hubSlug);
        var spaceTask = Space is not null
            ? Task.FromResult(GrpcResult<SpaceInfo>.Ok(Space))
            : _apiClient.GetSpaceBySlugResultAsync(slug);

        await Task.WhenAll(hubTask, spaceTask);

        Hub = hubTask.Result.IsSuccess ? hubTask.Result.Value : null;
        Space = spaceTask.Result.IsSuccess ? spaceTask.Result.Value : null;

        if (Space is not null)
        {
            SidebarScopeId = Space.PublicId;
            ResolveSidebarData();

            Discussions = await _apiClient.GetDiscussionsBySpaceAsync(Space.PublicId, 0, 20);
        }

        return Page();
    }

    private void ResolveSidebarData()
    {
        if (ShowTrendingDiscussions)
            InlineTrendingDiscussions = prefetchCache.ResolveOrPrefetch(
                $"trending-discussions:{SidebarScopeType}:{SidebarScopeId}",
                () => _apiClient.GetTopActiveDiscussionsTodayAsync(spaceId: SidebarScopeId),
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
    }
}
