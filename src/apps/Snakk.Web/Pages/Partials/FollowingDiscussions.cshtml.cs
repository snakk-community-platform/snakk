using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Snakk.Protos.Discussion;
using Snakk.Web.Helpers;
using Snakk.Web.Services;

namespace Snakk.Web.Pages.Partials;

public class FollowingDiscussionsModel(
    SnakkApiClient apiClient,
    ICommunityContext communityContext) : PageModel
{
    public IList<RecentDiscussionInfo> Items { get; set; } = [];
    public bool HasMoreItems { get; set; }
    public int Offset { get; set; }
    public int NextOffset { get; set; }
    public bool ShowCommunity { get; set; }
    public ICommunityContext Community => communityContext;

    public async Task<IActionResult> OnGetAsync(int offset = 0, int pageSize = 10)
    {
        if (!HttpContext.Request.Cookies.ContainsKey(AuthCookieHelper.AccessCookieName))
            return Content("", "text/html");

        pageSize = Math.Clamp(pageSize, 1, 20);
        Offset = offset;

        ShowCommunity = communityContext.IsMultiCommunityEnabled
            && string.IsNullOrEmpty(communityContext.CommunitySlug)
            && !communityContext.IsCustomDomain;

        try
        {
            var allIds = await apiClient.GetFollowedDiscussionsAsync();
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
