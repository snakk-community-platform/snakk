using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Snakk.Shared.Helpers;
using Snakk.Web.Services;
using Snakk.Web.Pages.ViewModels;
using Snakk.Protos.Community;
using Snakk.Protos.Hub;
using Snakk.Protos.Space;
using Snakk.Protos.Discussion;
using Snakk.Protos.Post;

namespace Snakk.Web.Pages.Discussions;

public class DetailModel(
    SnakkApiClient apiClient,
    IConfiguration configuration,
    ICommunityContext communityContext,
    IPrefetchCacheService prefetchCache) : BasePageModel(configuration, communityContext)
{
    private readonly SnakkApiClient _apiClient = apiClient;

    public DiscussionInfo? Discussion { get; set; }
    public PagedEnrichedPostList? Posts { get; set; }
    public HubInfo? Hub { get; set; }
    public SpaceInfo? Space { get; set; }
    public SpaceStats? SpaceStats { get; set; }
    public CommunityInfo? CommunityDetail { get; set; }
    public string HubSlug { get; set; } = string.Empty;
    public string SpaceSlug { get; set; } = string.Empty;
    public string SlugWithId { get; set; } = string.Empty;
    public string PublicId { get; set; } = string.Empty;

    // Current authenticated user info
    public bool IsAuthenticated { get; set; }
    public string? CurrentUserId { get; set; }
    public string? CurrentUserDisplayName { get; set; }
    public bool PreferEndlessScroll { get; set; } = true;

    // Whether any post in the initial batch contains code blocks (for Prism.js loading)
    public bool HasCodeBlocks { get; set; }

    // Absolute canonical URL for this discussion (used for share/oEmbed)
    public string CanonicalUrl { get; private set; } = string.Empty;

    // Inline sidebar data (populated from cache, null = HTMX fallback)
    public SidebarSpaceRulesVM? InlineSpaceRules { get; set; }
    public SidebarModeratorsVM? InlineModerators { get; set; }

    [BindProperty]
    public string PostContent { get; set; } = string.Empty;

    [BindProperty]
    public string? ReplyToPostId { get; set; }

    private async Task<int?> CalculateFirstUnreadPostNumberAsync(string discussionPublicId)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return null;

        try
        {
            // Get read state from API
            var readState = await _apiClient.GetReadStateAsync(CurrentUserId, discussionPublicId);
            if (readState?.LastReadPostId is null)
                return null;

            // Calculate post number of last read post
            var postNumber = await _apiClient.GetPostNumberAsync(discussionPublicId, readState.LastReadPostId);
            return postNumber + 1; // Return first unread (next post after last read)
        }
        catch
        {
            return null; // On error, fall back to page 1
        }
    }

    public async Task<IActionResult> OnGetAsync(
        string hubSlug,
        string spaceSlug,
        string slugWithId,
        int offset = 0,
        bool gotoUnread = false)
    {
        HubSlug = hubSlug;
        SpaceSlug = spaceSlug;
        SlugWithId = slugWithId;

        // Parse slug~publicId format
        var parts = slugWithId.Split('~');
        if (parts.Length != 2)
        {
            return NotFound();
        }

        PublicId = UlidBase62.Decode(parts[1]);

        try
        {
            // Read scroll preference from cookie (always available, no API call)
            PreferEndlessScroll = AuthCookieHelper.GetPreferEndlessScroll(HttpContext);

            // Load user info for auth-dependent features
            var user = await _apiClient.GetCurrentUserAsync();
            IsAuthenticated = user is not null;
            CurrentUserId = user?.PublicId;
            CurrentUserDisplayName = user?.DisplayName;

            // Handle gotoUnread redirect
            if (gotoUnread && IsAuthenticated && !string.IsNullOrEmpty(CurrentUserId))
            {
                var firstUnreadPostNumber = await CalculateFirstUnreadPostNumberAsync(PublicId);
                if (firstUnreadPostNumber.HasValue && firstUnreadPostNumber.Value > 1)
                {
                    // Calculate page and redirect with anchor
                    var page = ((firstUnreadPostNumber.Value - 1) / 20) + 1;
                    var newOffset = (page - 1) * 20;
                    var redirectUrl = Url.Page(
                        "/Discussions/Detail",
                        new { hubSlug, spaceSlug, slugWithId, offset = newOffset });

                    redirectUrl += $"#post-{firstUnreadPostNumber.Value}";
                    return Redirect(redirectUrl);
                }
                // If no unread or calculation failed, fall through to normal rendering
            }

            // Load hub, space, community, discussion, and posts
            var hubTask = _apiClient.GetHubBySlugAsync(hubSlug, CommunityContext.CommunitySlug!);
            var spaceTask = _apiClient.GetSpaceBySlugAsync(spaceSlug, hubSlug);
            var communityTask = !string.IsNullOrEmpty(CommunityContext.CommunitySlug)
                ? _apiClient.GetCommunityBySlugAsync(CommunityContext.CommunitySlug)
                : Task.FromResult<CommunityInfo?>(null);
            await Task.WhenAll(hubTask, spaceTask, communityTask);

            Hub = hubTask.Result;
            Space = spaceTask.Result;
            CommunityDetail = communityTask.IsCompletedSuccessfully ? communityTask.Result : null;

            if (Space is not null)
                SpaceStats = await _apiClient.GetSpaceStatsAsync(Space.PublicId);

            // Prefetch space rules and moderators for sidebar (inline if cache warm, HTMX fallback if cold)
            if (Space?.HasRules == true)
            {
                InlineSpaceRules = prefetchCache.ResolveOrPrefetch($"space-rules:{Space.PublicId}",
                    () => _apiClient.GetSpaceRulesAsync(Space.PublicId),
                    d => new SidebarSpaceRulesVM(d, CommunityContext, HubSlug, CommunityContext.CommunitySlug ?? "",
                        Space.ParentHubHasRules, Space.ParentCommunityHasRules, "cache"));
            }

            if (Space is not null)
            {
                InlineModerators = prefetchCache.ResolveOrPrefetch(
                    $"moderators:Space:{Space.PublicId}",
                    () => _apiClient.GetModeratorsAsync("Space", Space.PublicId),
                    d => new SidebarModeratorsVM(
                        d,
                        $"{Helpers.SnakkUrlHelper.Space(CommunityContext, HubSlug, SpaceSlug)}/moderators",
                        "cache"));
            }

            var discussionResult = await _apiClient.GetDiscussionResultAsync(PublicId);
            if (!discussionResult.IsSuccess)
                return discussionResult.Status == GrpcStatus.NotFound ? NotFound() : StatusCode(503);

            Discussion = discussionResult.Value;
            CanonicalUrl = $"{Request.Scheme}://{Request.Host}{Request.Path}";

            Posts = await _apiClient.GetDiscussionPostsAsync(PublicId, offset, 20);
            HasCodeBlocks = Posts?.HasCodeBlocks ?? false;
        }
        catch
        {
            return NotFound();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string hubSlug, string spaceSlug, string slugWithId)
    {
        HubSlug = hubSlug;
        SpaceSlug = spaceSlug;
        SlugWithId = slugWithId;

        var parts = slugWithId.Split('~');
        if (parts.Length != 2)
        {
            return NotFound();
        }

        PublicId = UlidBase62.Decode(parts[1]);

        // Load auth status
        var authStatus = await _apiClient.GetAuthStatusAsync();
        IsAuthenticated = authStatus?.IsAuthenticated ?? false;
        CurrentUserId = authStatus?.PublicId;
        CurrentUserDisplayName = authStatus?.DisplayName;

        // Require authentication to post
        if (!IsAuthenticated || string.IsNullOrEmpty(CurrentUserId))
        {
            return RedirectToPage("/Auth/Login", new { returnUrl = $"/h/{hubSlug}/{spaceSlug}/{slugWithId}" });
        }

        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(PostContent))
        {
            // Reload page data for re-render
            Discussion = await _apiClient.GetDiscussionAsync(PublicId);
            Posts = await _apiClient.GetDiscussionPostsAsync(PublicId, 0, 20);

            return Page();
        }

        try
        {
            await _apiClient.CreatePostAsync(
                PublicId,
                PostContent,
                string.IsNullOrEmpty(ReplyToPostId) ? null : ReplyToPostId);

            return RedirectToPage("/Discussions/Detail", null, new { hubSlug, spaceSlug, slugWithId }, "reply-form");
        }
        catch
        {
            ModelState.AddModelError("", "Failed to create post");
            // Reload page data for re-render
            Discussion = await _apiClient.GetDiscussionAsync(PublicId);
            Posts = await _apiClient.GetDiscussionPostsAsync(PublicId, 0, 20);

            return Page();
        }
    }
}
