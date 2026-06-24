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
    public Dictionary<string, DateTime> VisitedTimestamps { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(string? ids, string? timestamps, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(ids))
            return Content("", "text/html");

        ShowCommunity = communityContext.ShouldShowCommunity();

        var idList = ids.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Take(20)
            .ToList();

        if (!string.IsNullOrWhiteSpace(timestamps))
        {
            var tsList = timestamps.Split(',', StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < Math.Min(idList.Count, tsList.Length); i++)
            {
                if (DateTime.TryParse(tsList[i], out var dt))
                    VisitedTimestamps[idList[i]] = dt.ToUniversalTime();
            }
        }

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
