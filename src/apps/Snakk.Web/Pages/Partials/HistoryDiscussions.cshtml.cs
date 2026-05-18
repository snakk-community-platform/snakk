using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Snakk.Protos.Discussion;
using Snakk.Web.Services;

namespace Snakk.Web.Pages.Partials;

public class HistoryDiscussionsModel(
    SnakkApiClient apiClient,
    ICommunityContext communityContext) : PageModel
{
    public IList<RecentDiscussionInfo> Items { get; set; } = [];
    public bool ShowCommunity { get; set; }
    public ICommunityContext Community => communityContext;

    public async Task<IActionResult> OnGetAsync(string? ids, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(ids))
            return Content("", "text/html");

        ShowCommunity = communityContext.IsMultiCommunityEnabled
            && string.IsNullOrEmpty(communityContext.CommunitySlug)
            && !communityContext.IsCustomDomain;

        var idList = ids.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Take(20)
            .ToList();

        try
        {
            var result = await apiClient.GetRecentDiscussionsByIdsAsync(idList);
            var fetched = result?.Items ?? [];
            // Preserve the requested order (newest-visited first from localStorage)
            var dict = fetched.ToDictionary(i => i.PublicId);
            Items = idList.Where(dict.ContainsKey).Select(id => dict[id]).ToList();
        }
        catch { }

        return Page();
    }
}
