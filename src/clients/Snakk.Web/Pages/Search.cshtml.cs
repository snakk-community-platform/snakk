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
    public string Tab { get; set; } = "discussions";

    public PagedResult<DiscussionSearchResultDto>? Discussions { get; set; }
    public PagedResult<PostSearchResultDto>? Posts { get; set; }
    public UserProfileDto? FilteredUser { get; set; }
    public bool PreferEndlessScroll { get; set; } = true;

    public string BuildSearchUrl(string? query = null, string? authorPublicId = null, string? tab = null, int offset = 0)
    {
        var parameters = new List<string>();
        var q = query ?? Q;
        var author = authorPublicId ?? AuthorPublicId;
        var t = tab ?? Tab;

        if (!string.IsNullOrEmpty(q)) parameters.Add($"q={Uri.EscapeDataString(q)}");
        if (!string.IsNullOrEmpty(author)) parameters.Add($"authorPublicId={author}");
        if (t != "discussions") parameters.Add($"tab={t}");
        if (offset > 0) parameters.Add($"offset={offset}");

        return parameters.Count > 0 ? $"/search?{string.Join("&", parameters)}" : "/search";
    }

    public async Task OnGetAsync(int offset = 0)
    {
        // Fetch user preference if authenticated
        var userTask = _apiClient.GetCurrentUserAsync();

        // If filtering by author, get their profile
        Task<UserProfileDto?>? filteredUserTask = null;
        if (!string.IsNullOrEmpty(AuthorPublicId))
        {
            filteredUserTask = _apiClient.GetUserProfileAsync(AuthorPublicId);
        }

        if (Tab == "posts")
        {
            Posts = await _apiClient.SearchPostsAsync(Q, AuthorPublicId, offset: offset, pageSize: 20);
        }
        else
        {
            Discussions = await _apiClient.SearchDiscussionsAsync(Q, AuthorPublicId, offset: offset, pageSize: 20);
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
    }
}
