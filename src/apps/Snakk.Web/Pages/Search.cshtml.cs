using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Snakk.Web.Services;
using Snakk.Protos.Search;
using Snakk.Protos.User;

namespace Snakk.Web.Pages;

public class SearchModel(
    SnakkApiClient apiClient,
    IConfiguration configuration,
    ICommunityContext communityContext) : BasePageModel(configuration, communityContext)
{
    private readonly SnakkApiClient _apiClient = apiClient;

    [BindProperty(SupportsGet = true)]
    public string? Q { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? AuthorPublicId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? HubPublicId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SpacePublicId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? DiscussionPublicId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string SearchType { get; set; } = "discussion";

    [BindProperty(SupportsGet = true)]
    public string? DateRange { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SortBy { get; set; }

    // Deprecated: kept for backward compatibility with old URLs
    [BindProperty(SupportsGet = true)]
    public string? Tab { get; set; }

    public PagedDiscussionSearchResults? Discussions { get; set; }
    public PagedPostSearchResults? Posts { get; set; }
    public UserProfileInfo? FilteredUser { get; set; }
    public bool HasSearchError { get; set; }
    public bool IsScrollAppend { get; set; }
    public int MaxOffset { get; set; }

    public string BuildSearchUrl(
        string? query = null,
        string? authorPublicId = null,
        string? searchType = null,
        int offset = 0,
        string? dateRange = null,
        string? sortBy = null)
    {
        var parameters = new List<string>();
        var q = query ?? Q;
        var author = authorPublicId ?? AuthorPublicId;
        var type = searchType ?? SearchType;
        var range = dateRange ?? DateRange;
        var sort = sortBy ?? SortBy;

        if (!string.IsNullOrEmpty(q)) parameters.Add($"q={Uri.EscapeDataString(q)}");
        if (!string.IsNullOrEmpty(author)) parameters.Add($"authorPublicId={author}");
        if (!string.IsNullOrEmpty(type) && type != "discussion") parameters.Add($"searchType={type}");
        if (!string.IsNullOrEmpty(range)) parameters.Add($"dateRange={Uri.EscapeDataString(range)}");
        if (!string.IsNullOrEmpty(sort) && sort != "newest") parameters.Add($"sortBy={Uri.EscapeDataString(sort)}");
        if (offset > 0) parameters.Add($"offset={offset}");

        return parameters.Count > 0 ? $"/search?{string.Join("&", parameters)}" : "/search";
    }

    public async Task<IActionResult> OnGetAsync(int offset = 0, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Backward compatibility: map old "tab" parameter to new "searchType"
        if (!string.IsNullOrEmpty(Tab))
        {
            SearchType = Tab == "posts" ? "post" : "discussion";
        }

        // Normalize search type
        SearchType = SearchType?.ToLowerInvariant() ?? "discussion";

        var maxPages = configuration.GetValue("EndlessScroll:MaxPages", 10);
        MaxOffset = maxPages * 20;

        var viewerAllowsAdult = await AdultContentGate.ViewerAllowsAdultAsync(HttpContext, _apiClient);

        // If filtering by author, get their profile
        Task<UserProfileInfo?>? filteredUserTask = null;
        if (!string.IsNullOrEmpty(AuthorPublicId))
        {
            filteredUserTask = _apiClient.GetUserProfileAsync(AuthorPublicId);
        }

        // Execute search based on type
        try
        {
        switch (SearchType)
        {
            case "post":
                Posts = await _apiClient.SearchPostsAsync(
                    Q,
                    AuthorPublicId,
                    DiscussionPublicId,
                    SpacePublicId,
                    offset: offset,
                    pageSize: 20,
                    sortBy: SortBy,
                    dateRange: DateRange);
                break;

            case "discussion":
                Discussions = await _apiClient.SearchDiscussionsAsync(
                    Q,
                    AuthorPublicId,
                    SpacePublicId,
                    HubPublicId,
                    offset: offset,
                    pageSize: 20,
                    viewerAllowsAdult: viewerAllowsAdult,
                    sortBy: SortBy,
                    dateRange: DateRange);
                break;

            default:
                // Default to discussion search
                Discussions = await _apiClient.SearchDiscussionsAsync(
                    Q,
                    AuthorPublicId,
                    SpacePublicId,
                    HubPublicId,
                    offset: offset,
                    pageSize: 20,
                    viewerAllowsAdult: viewerAllowsAdult,
                    sortBy: SortBy,
                    dateRange: DateRange);
                break;
        }
        }
        catch (Exception)
        {
            HasSearchError = true;
        }

        // Await filtered user task if active
        if (filteredUserTask is not null)
        {
            try { await filteredUserTask; } catch { }
        }

        if (filteredUserTask?.IsCompletedSuccessfully == true)
        {
            FilteredUser = filteredUserTask.Result;
        }

        // If this is an HTMX request, return just the partial view.
        // Exception: boost navigations and history restores target #main-content and expect
        // the full page structure (sidebar etc.) — let those fall through to Page().
        if (Request.Headers.ContainsKey("HX-Request"))
        {
            var isPageNavigation = Request.Headers.ContainsKey("HX-Boosted")
                || Request.Headers.ContainsKey("HX-History-Restore-Request");

            if (!isPageNavigation)
            {
                IsScrollAppend = offset > 0;
                return Partial("_SearchResults", this);
            }
        }

        return Page();
    }
}
