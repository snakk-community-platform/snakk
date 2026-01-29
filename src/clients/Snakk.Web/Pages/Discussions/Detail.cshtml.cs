using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Snakk.Web.Models;
using Snakk.Web.Services;
using Snakk.Infrastructure.Rendering;

namespace Snakk.Web.Pages.Discussions;

public class DetailModel(SnakkApiClient apiClient, IMarkupParser markupParser, IConfiguration configuration, ICommunityContext communityContext) : BasePageModel(configuration, communityContext)
{
    private readonly SnakkApiClient _apiClient = apiClient;
    private readonly IMarkupParser _markupParser = markupParser;

    public DiscussionDto? Discussion { get; set; }
    public PagedResult<PostDto>? Posts { get; set; }
    public HubDetailDto? Hub { get; set; }
    public SpaceDetailDto? Space { get; set; }
    public string HubSlug { get; set; } = string.Empty;
    public string SpaceSlug { get; set; } = string.Empty;
    public string SlugWithId { get; set; } = string.Empty;
    public string PublicId { get; set; } = string.Empty;

    // Current authenticated user info
    public bool IsAuthenticated { get; set; }
    public string? CurrentUserId { get; set; }
    public string? CurrentUserDisplayName { get; set; }
    public bool PreferEndlessScroll { get; set; } = true;

    [BindProperty]
    public string PostContent { get; set; } = string.Empty;

    [BindProperty]
    public string? ReplyToPostId { get; set; }

    public string RenderMarkup(string content) => _markupParser.ToHtml(content);

    private async Task<int?> CalculateFirstUnreadPostNumberAsync(string discussionPublicId)
    {
        if (string.IsNullOrEmpty(CurrentUserId))
            return null;

        try
        {
            // Get read state from API
            var readState = await _apiClient.GetReadStateAsync(CurrentUserId, discussionPublicId);
            if (readState?.LastReadPostId == null)
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

    public async Task<IActionResult> OnGetAsync(string hubSlug, string spaceSlug, string slugWithId, int offset = 0, bool gotoUnread = false)
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

        PublicId = parts[1];

        try
        {
            // Load user info first
            var user = await _apiClient.GetCurrentUserAsync();
            IsAuthenticated = user != null;
            CurrentUserId = user?.PublicId;
            CurrentUserDisplayName = user?.DisplayName;
            PreferEndlessScroll = user?.PreferEndlessScroll ?? true;

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

            // Load hub, space, discussion, and posts
            var hubTask = _apiClient.GetHubBySlugAsync(hubSlug);
            var spaceTask = _apiClient.GetSpaceBySlugAsync(spaceSlug);
            await Task.WhenAll(hubTask, spaceTask);

            Hub = hubTask.Result;
            Space = spaceTask.Result;

            Discussion = await _apiClient.GetDiscussionAsync(PublicId);
            if (Discussion == null)
            {
                return NotFound();
            }

            Posts = await _apiClient.GetDiscussionPostsAsync(PublicId, offset, 20);
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

        PublicId = parts[1];

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
            var request = new CreatePostRequest(
                DiscussionId: PublicId,
                UserId: CurrentUserId,
                Content: PostContent,
                ReplyToPostId: string.IsNullOrEmpty(ReplyToPostId) ? null : ReplyToPostId);

            await _apiClient.CreatePostAsync(request);

            return RedirectToPage("/Discussions/Detail", new { hubSlug, spaceSlug, slugWithId });
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
