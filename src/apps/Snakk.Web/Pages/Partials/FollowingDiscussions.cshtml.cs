using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Snakk.Protos.Discussion;
using Snakk.Web.Helpers;
using Snakk.Web.Services;

namespace Snakk.Web.Pages.Partials;

public class FollowingDiscussionsModel(
    SnakkApiClient apiClient,
    IConfiguration configuration,
    ICommunityContext communityContext) : PageModel
{
    public IList<RecentDiscussionInfo> Items { get; set; } = [];
    public bool HasMoreItems { get; set; }
    public int Offset { get; set; }
    public int NextOffset { get; set; }
    public int MaxOffset { get; set; }
    public bool ShowCommunity { get; set; }
    public ICommunityContext Community => communityContext;
    public Dictionary<string, DateTime> FollowTimestamps { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(int offset = 0, int pageSize = 10, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!HttpContext.Request.Cookies.ContainsKey(AuthCookieHelper.AccessCookieName))
            return Content("", "text/html");

        pageSize = Math.Clamp(pageSize, 1, 20);
        Offset = offset;

        var maxPages = configuration.GetValue("EndlessScroll:MaxPages", 10);
        MaxOffset = maxPages * pageSize;

        if (offset >= MaxOffset)
        {
            Items = [];
            HasMoreItems = false;
            return Page();
        }

        ShowCommunity = communityContext.IsMultiCommunityEnabled
            && string.IsNullOrEmpty(communityContext.CommunitySlug)
            && !communityContext.IsCustomDomain;

        try
        {
            var allDetails = await apiClient.GetFollowedDiscussionsDetailedAsync(cancellationToken);
            FollowTimestamps = allDetails.ToDictionary(x => x.PublicId, x => x.FollowedAt);
            var allIds = allDetails.Select(x => x.PublicId).ToList();
            var batch = allIds.Skip(offset).Take(pageSize).ToList();

            HasMoreItems = offset + pageSize < allIds.Count;
            NextOffset = offset + pageSize;

            if (batch.Count > 0)
            {
                var result = await apiClient.GetRecentDiscussionsByIdsAsync(batch);
                Items = result?.Items ?? [];
            }
        }
        catch { }

        return Page();
    }
}
