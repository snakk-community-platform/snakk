using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Snakk.Web.Models;
using Snakk.Web.Services;

namespace Snakk.Web.Pages;

public class SearchModel(SnakkApiClient apiClient, IConfiguration configuration, ICommunityContext communityContext) : BasePageModel(configuration, communityContext)
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
    public string SearchType { get; set; } = "post";

    [BindProperty(SupportsGet = true)]
    public string? DateRange { get; set; }

    // Deprecated: kept for backward compatibility with old URLs
    [BindProperty(SupportsGet = true)]
    public string? Tab { get; set; }

    public PagedResult<DiscussionSearchResultDto>? Discussions { get; set; }
    public PagedResult<PostSearchResultDto>? Posts { get; set; }
    public PagedResult<dynamic>? Spaces { get; set; } // TODO: Replace with SpaceSearchResultDto when available
    public PagedResult<UserProfileDto>? Users { get; set; }
    public UserProfileDto? FilteredUser { get; set; }
    public bool PreferEndlessScroll { get; set; } = true;

    public string BuildSearchUrl(string? query = null, string? authorPublicId = null, string? searchType = null, int offset = 0, string? dateRange = null)
    {
        var parameters = new List<string>();
        var q = query ?? Q;
        var author = authorPublicId ?? AuthorPublicId;
        var type = searchType ?? SearchType;
        var range = dateRange ?? DateRange;

        if (!string.IsNullOrEmpty(q)) parameters.Add($"q={Uri.EscapeDataString(q)}");
        if (!string.IsNullOrEmpty(author)) parameters.Add($"authorPublicId={author}");
        if (!string.IsNullOrEmpty(type) && type != "post") parameters.Add($"searchType={type}");
        if (!string.IsNullOrEmpty(range)) parameters.Add($"dateRange={Uri.EscapeDataString(range)}");
        if (offset > 0) parameters.Add($"offset={offset}");

        return parameters.Count > 0 ? $"/search?{string.Join("&", parameters)}" : "/search";
    }

    public async Task<IActionResult> OnGetAsync(int offset = 0)
    {
        // Backward compatibility: map old "tab" parameter to new "searchType"
        if (!string.IsNullOrEmpty(Tab))
        {
            SearchType = Tab == "posts" ? "post" : "discussion";
        }

        // Normalize search type
        SearchType = SearchType?.ToLowerInvariant() ?? "post";

        // Fetch user preference if authenticated
        var userTask = _apiClient.GetCurrentUserAsync();

        // If filtering by author, get their profile
        Task<UserProfileDto?>? filteredUserTask = null;
        if (!string.IsNullOrEmpty(AuthorPublicId))
        {
            filteredUserTask = _apiClient.GetUserProfileAsync(AuthorPublicId);
        }

        // Execute search based on type
        switch (SearchType)
        {
            case "post":
                Posts = await _apiClient.SearchPostsAsync(
                    Q,
                    AuthorPublicId,
                    DiscussionPublicId,
                    SpacePublicId,
                    offset: offset,
                    pageSize: 20);
                break;

            case "discussion":
                Discussions = await _apiClient.SearchDiscussionsAsync(
                    Q,
                    AuthorPublicId,
                    SpacePublicId,
                    HubPublicId,
                    offset: offset,
                    pageSize: 20);
                break;

            case "space":
                // TODO: Implement space search when API is available
                Spaces = new PagedResult<dynamic>(
                    Items: [],
                    Offset: offset,
                    PageSize: 20,
                    HasMoreItems: false);
                break;

            case "user":
                // TODO: Implement user search when API is available
                Users = new PagedResult<UserProfileDto>(
                    Items: [],
                    Offset: offset,
                    PageSize: 20,
                    HasMoreItems: false);
                break;

            default:
                // Default to discussion search
                Discussions = await _apiClient.SearchDiscussionsAsync(
                    Q,
                    AuthorPublicId,
                    SpacePublicId,
                    HubPublicId,
                    offset: offset,
                    pageSize: 20);
                break;
        }

        // Await remaining tasks
        try
        {
            await Task.WhenAll(
                userTask,
                filteredUserTask ?? Task.CompletedTask);
        }
        catch
        {
            // Continue with whatever succeeded
        }

        var user = userTask.IsCompletedSuccessfully ? userTask.Result : null;
        PreferEndlessScroll = user?.PreferEndlessScroll ?? true;

        if (filteredUserTask?.IsCompletedSuccessfully == true)
        {
            FilteredUser = filteredUserTask.Result;
        }

        // If this is an HTMX request, return just the partial view
        if (Request.Headers.ContainsKey("HX-Request"))
        {
            return Partial("_SearchResults", this);
        }

        return Page();
    }
}
