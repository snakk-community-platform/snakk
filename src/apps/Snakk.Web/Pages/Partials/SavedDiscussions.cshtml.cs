using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Snakk.Protos.Discussion;
using Snakk.Web.Helpers;
using Snakk.Web.Services;

namespace Snakk.Web.Pages.Partials;

public class SavedDiscussionsModel(
    SnakkApiClient apiClient,
    ICommunityContext communityContext) : PageModel
{
    public ICommunityContext Community => communityContext;
    public IList<RecentDiscussionInfo> Items { get; set; } = [];
    public bool HasMoreItems { get; set; }
    public int Offset { get; set; }
    public int NextOffset { get; set; }
    public bool ShowCommunity { get; set; }

    public async Task<IActionResult> OnGetAsync(int offset = 0, int pageSize = 20)
    {
        if (!HttpContext.Request.Cookies.ContainsKey(AuthCookieHelper.AccessCookieName))
            return Content("", "text/html");

        pageSize = Math.Clamp(pageSize, 1, 50);
        Offset = offset;

        ShowCommunity = communityContext.IsMultiCommunityEnabled
            && string.IsNullOrEmpty(communityContext.CommunitySlug)
            && !communityContext.IsCustomDomain;

        try
        {
            var result = await apiClient.GetSavedDiscussionsAsync(offset, pageSize);
            if (result != null)
            {
                Items = result.Items;
                HasMoreItems = result.HasMoreItems;
                NextOffset = offset + result.Items.Count;
            }
        }
        catch { }

        return Page();
    }
}
