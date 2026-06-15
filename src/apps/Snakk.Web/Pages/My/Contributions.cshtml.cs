using Microsoft.AspNetCore.Mvc;
using Snakk.Protos.Discussion;
using Snakk.Web.Pages.ViewModels;
using Snakk.Web.Services;
using System.Security.Claims;

namespace Snakk.Web.Pages.My;

public class ContributionsModel(
    SnakkApiClient apiClient,
    IConfiguration configuration,
    ICommunityContext communityContext,
    ILogger<ContributionsModel> logger) : BasePageModel(configuration, communityContext)
{
    private readonly SnakkApiClient _apiClient = apiClient;
    private const int PageSize = 20;

    public string ActiveTab { get; set; } = "discussions";
    public int Offset { get; set; }
    public bool IsAuthenticated { get; set; }

    public PagedRecentDiscussionList? Discussions { get; set; }
    public List<PostListItemVM>? Posts { get; set; }
    public bool PostsHasMore { get; set; }

    public async Task<IActionResult> OnGetAsync(
        string? tab,
        [FromQuery] int offset = 0,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Request.Query.TryGetValue("tab", out var queryTab) && !string.IsNullOrEmpty(queryTab))
            return RedirectToPage(new { tab = queryTab.ToString() });

        IsAuthenticated = HttpContext.Request.Cookies.ContainsKey(AuthCookieHelper.AccessCookieName);
        if (!IsAuthenticated) return Page();

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Page();

        ActiveTab = tab == "posts" ? "posts" : "discussions";
        Offset = Math.Max(0, offset);

        if (ActiveTab == "posts")
        {
            try
            {
                var result = await _apiClient.SearchPostsAsync(
                    authorPublicId: userId, offset: Offset, pageSize: PageSize);
                if (result != null)
                {
                    Posts = result.Items.Select(PostListItemVM.FromSearchResult).ToList();
                    PostsHasMore = result.HasMoreItems;
                }
            }
            catch (Exception ex) { logger.LogWarning(ex, "Failed to load contributed posts"); }
        }
        else
        {
            var viewerAllowsAdult = await AdultContentGate.ViewerAllowsAdultAsync(HttpContext, _apiClient);
            try
            {
                Discussions = await _apiClient.GetRecentDiscussionsAsync(
                    offset: Offset, pageSize: PageSize, authorId: userId, viewerAllowsAdult: viewerAllowsAdult);
            }
            catch (Exception ex) { logger.LogWarning(ex, "Failed to load contributed discussions"); }
        }

        return Page();
    }
}
