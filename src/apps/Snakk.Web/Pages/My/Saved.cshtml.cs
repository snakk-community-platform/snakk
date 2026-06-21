using Microsoft.AspNetCore.Mvc;
using Snakk.Protos.Discussion;
using Snakk.Protos.Save;
using Snakk.Web.Helpers;
using Snakk.Web.Services;

namespace Snakk.Web.Pages.My;

public class SavedModel(
    SnakkApiClient apiClient,
    IConfiguration configuration,
    ICommunityContext communityContext,
    ILogger<SavedModel> logger) : BasePageModel(configuration, communityContext)
{
    private readonly SnakkApiClient _apiClient = apiClient;

    private const int PageSize = 20;

    public string ActiveTab { get; set; } = "discussions";
    public int DiscussionCount { get; set; }
    public int PostCount { get; set; }
    public int Offset { get; set; }

    public PagedRecentDiscussionList? SavedDiscussions { get; set; }
    public PagedSavedPostsList? SavedPosts { get; set; }

    public bool IsAuthenticated { get; set; }

    public async Task<IActionResult> OnGetAsync(
        string? tab,
        [FromQuery] int offset = 0,
        CancellationToken cancellationToken = default)
    {
        Preload("discussion-card");
        cancellationToken.ThrowIfCancellationRequested();
        if (Request.Query.TryGetValue("tab", out var queryTab) && !string.IsNullOrEmpty(queryTab))
            return RedirectToPage(new { tab = queryTab.ToString() });

        IsAuthenticated = HttpContext.Request.Cookies.ContainsKey(AuthCookieHelper.AccessCookieName);
        if (!IsAuthenticated) return Page();

        ActiveTab = tab == "posts" ? "posts" : "discussions";
        Offset = Math.Max(0, offset);

        // Load counts for tab badges
        try
        {
            var counts = await _apiClient.GetSaveCountsAsync();
            if (counts != null)
            {
                DiscussionCount = counts.DiscussionCount;
                PostCount = counts.PostCount;
            }
        }
        catch (Exception ex) { logger.LogWarning(ex, "Failed to load save counts"); }

        // Load active tab content
        if (ActiveTab == "posts")
        {
            try { SavedPosts = await _apiClient.GetSavedPostsAsync(Offset, PageSize); }
            catch (Exception ex) { logger.LogWarning(ex, "Failed to load saved posts"); }
        }
        else
        {
            try { SavedDiscussions = await _apiClient.GetSavedDiscussionsAsync(Offset, PageSize); }
            catch (Exception ex) { logger.LogWarning(ex, "Failed to load saved discussions"); }
        }

        return Page();
    }
}
