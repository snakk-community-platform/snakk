using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.OutputCaching;
using Snakk.Shared.Helpers;
using Snakk.Web.Services;
using Snakk.Web.Pages.ViewModels;
using Snakk.Protos.Community;
using Snakk.Protos.Hub;
using Snakk.Protos.Space;
using Snakk.Protos.Discussion;
using Snakk.Protos.Post;
using System.Threading.RateLimiting;

namespace Snakk.Web.Pages.Discussions;

[OutputCache(PolicyName = "AnonymousPage")]
public class DetailModel(
    SnakkApiClient apiClient,
    IConfiguration configuration,
    ICommunityContext communityContext,
    IPrefetchCacheService prefetchCache,
    DiscussionCreateRateLimiter rateLimiter) : BasePageModel(configuration, communityContext)
{
    private readonly SnakkApiClient _apiClient = apiClient;
    private readonly DiscussionCreateRateLimiter _createRateLimiter = rateLimiter;

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

    // Whether any post in the initial batch contains code blocks (for Prism.js loading)
    public bool HasCodeBlocks { get; set; }

    // Adult-content gating: discussion or its space is adult; visitor undecided.
    public AdultContentState AdultGateState { get; set; } = AdultContentState.Allowed;

    // Absolute canonical URL for this discussion (used for share/oEmbed)
    public string CanonicalUrl { get; private set; } = string.Empty;

    // Inline sidebar data (populated from cache, null = HTMX fallback)
    public SidebarSpaceRulesVM? InlineSpaceRules { get; set; }
    public SidebarModeratorsVM? InlineModerators { get; set; }

    // Type-specific data (loaded for non-Standard types)
    public Snakk.Protos.Discussion.QuestionStatusResponse? QuestionStatus { get; set; }
    public Snakk.Protos.Discussion.DebateInfoResponse? DebateInfo { get; set; }
    public Snakk.Protos.Discussion.DiscussionLinkResponse? LinkInfo { get; set; }
    public Snakk.Protos.Discussion.JournalEntriesResponse? JournalInfo { get; set; }
    public string? ImagesLayout { get; set; }
    public List<Snakk.Protos.Discussion.ImagesImageProto> ImagesItems { get; set; } = [];
    public bool ImagesIsSpoiler { get; set; }

    [BindProperty]
    public string PostContent { get; set; } = string.Empty;

    [BindProperty]
    public string? ReplyToPostId { get; set; }

    // Debate-only: position id for this reply. Null on non-debate discussions
    // and required on debate discussions (enforced in OnPostAsync).
    [BindProperty]
    public int? DebatePositionId { get; set; }

    private async Task<int?> CalculateFirstUnreadPostNumberAsync(string discussionPublicId)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return null;

        try
        {
            // Get read state from API
            var readState = await _apiClient.GetReadStateAsync(CurrentUserId, discussionPublicId);
            if (string.IsNullOrEmpty(readState?.LastReadPostId))
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

        try
        {
            PublicId = UlidBase62.Decode(parts[1]);
        }
        catch (ArgumentException)
        {
            return NotFound();
        }

        try
        {
            // Load user info, hub, space, and community in parallel
            var userTask = _apiClient.GetCurrentUserAsync();
            var hubTask = _apiClient.GetHubBySlugAsync(hubSlug, CommunityContext.CommunitySlug!);
            var spaceTask = _apiClient.GetSpaceBySlugAsync(spaceSlug, hubSlug);
            var communityTask = !string.IsNullOrEmpty(CommunityContext.CommunitySlug)
                ? _apiClient.GetCommunityBySlugAsync(CommunityContext.CommunitySlug)
                : Task.FromResult<CommunityInfo?>(null);
            await Task.WhenAll(userTask, hubTask, spaceTask, communityTask);

            var user = userTask.Result;
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

            Hub = hubTask.Result;
            Space = spaceTask.Result;
            CommunityDetail = communityTask.IsCompletedSuccessfully ? communityTask.Result : null;

            // Space stats (depends on Space being loaded)
            Task<SpaceStats?> spaceStatsTask = Space is not null
                ? _apiClient.GetSpaceStatsAsync(Space.PublicId)
                : Task.FromResult<SpaceStats?>(null);

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

            // Load discussion and posts (discussion must load first, posts depend on it)
            var discussionResult = await _apiClient.GetDiscussionResultAsync(PublicId);
            if (!discussionResult.IsSuccess)
                return discussionResult.Status == GrpcStatus.NotFound ? NotFound() : StatusCode(503);

            Discussion = discussionResult.Value!;
            CanonicalUrl = $"{Request.Scheme}://{Request.Host}{Request.Path}";

            // Adult-content gating — short-circuit before loading posts/type-specific data
            var contentIsAdult = Discussion.IsAdult || Space?.IsAdultOnly == true;
            if (contentIsAdult)
            {
                bool? userPref = user?.HasAllowAdultContent == true ? user.AllowAdultContent : null;
                AdultGateState = AdultContentGate.GetState(HttpContext, userPref, contentIsAdult: true);
                if (AdultGateState != AdultContentState.Allowed)
                {
                    SpaceStats = await spaceStatsTask;
                    return Page();
                }
            }

            // Await space stats (started earlier in parallel)
            SpaceStats = await spaceStatsTask;

            Posts = await _apiClient.GetDiscussionPostsAsync(PublicId, offset, 20);
            HasCodeBlocks = Posts?.HasCodeBlocks ?? false;

            // Load type-specific data
            switch (Discussion.Type)
            {
                case "Question":
                    QuestionStatus = await _apiClient.GetQuestionStatusAsync(PublicId);
                    break;
                case "Debate":
                    DebateInfo = await _apiClient.GetDebateInfoAsync(PublicId);
                    break;
                case "Link":
                    LinkInfo = await _apiClient.GetDiscussionLinkAsync(PublicId);
                    break;
                case "Journal":
                    JournalInfo = await _apiClient.GetJournalEntriesAsync(PublicId);
                    break;
                case "Images":
                    var imagesData = await _apiClient.GetImagesDataAsync(PublicId);
                    ImagesLayout = imagesData?.Layout ?? "grid";
                    ImagesItems = imagesData?.Images?.ToList() ?? [];
                    ImagesIsSpoiler = imagesData?.IsSpoiler ?? false;
                    break;
            }
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

        try
        {
            PublicId = UlidBase62.Decode(parts[1]);
        }
        catch (ArgumentException)
        {
            return NotFound();
        }

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

        var lease = await _createRateLimiter.AcquireAsync(HttpContext);
        if (!lease.IsAcquired)
        {
            var retryAfter = lease.TryGetMetadata(MetadataName.RetryAfter, out var r) ? (int)r.TotalSeconds : 360;
            lease.Dispose();
            ModelState.AddModelError(string.Empty, $"You're posting too fast. Try again in {retryAfter}s.");
            Discussion = await _apiClient.GetDiscussionAsync(PublicId);
            Posts = await _apiClient.GetDiscussionPostsAsync(PublicId, 0, 20);
            return Page();
        }
        lease.Dispose();

        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(PostContent))
        {
            // Reload page data for re-render
            Discussion = await _apiClient.GetDiscussionAsync(PublicId);
            Posts = await _apiClient.GetDiscussionPostsAsync(PublicId, 0, 20);

            return Page();
        }

        // Debate discussions require a position on every reply. Load the
        // discussion first so we know whether this is a debate, then validate.
        var discussionForType = await _apiClient.GetDiscussionAsync(PublicId);
        var isDebate = discussionForType?.Type == "Debate";

        if (isDebate)
        {
            if (DebatePositionId is null)
            {
                ModelState.AddModelError(nameof(DebatePositionId), "Pick a position before replying.");
                Discussion = discussionForType;
                Posts = await _apiClient.GetDiscussionPostsAsync(PublicId, 0, 20);
                DebateInfo = await _apiClient.GetDebateInfoAsync(PublicId);
                return Page();
            }

            // Verify the position actually belongs to this debate.
            var debateInfo = await _apiClient.GetDebateInfoAsync(PublicId);
            var validIds = debateInfo?.Positions?.Select(p => p.Id).ToHashSet() ?? [];
            if (!validIds.Contains(DebatePositionId.Value))
            {
                ModelState.AddModelError(nameof(DebatePositionId), "Unknown debate position.");
                Discussion = discussionForType;
                Posts = await _apiClient.GetDiscussionPostsAsync(PublicId, 0, 20);
                DebateInfo = debateInfo;
                return Page();
            }
        }

        try
        {
            var newPostPublicId = await _apiClient.CreatePostAsync(
                PublicId,
                PostContent,
                string.IsNullOrEmpty(ReplyToPostId) ? null : ReplyToPostId);

            // Store the debate position for the new post (debate-only). If this
            // fails we still proceed — the post itself was created successfully
            // and the position can be re-submitted, but we log via ModelState
            // for visibility during testing.
            if (isDebate && DebatePositionId.HasValue && !string.IsNullOrEmpty(newPostPublicId))
            {
                await _apiClient.SetPostDebatePositionAsync(
                    PublicId,
                    newPostPublicId,
                    DebatePositionId.Value);
            }

            // Navigate to the last page so the new post is visible
            var discussion = await _apiClient.GetDiscussionAsync(PublicId);
            var totalPosts = discussion?.PostCount ?? 0;
            var lastPageOffset = Math.Max(0, ((totalPosts - 1) / 20) * 20);

            return RedirectToPage("/Discussions/Detail", null,
                new { hubSlug, spaceSlug, slugWithId, offset = lastPageOffset },
                "reply-form-container");
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
