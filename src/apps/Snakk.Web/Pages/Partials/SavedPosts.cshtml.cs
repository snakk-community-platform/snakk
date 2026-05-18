using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Snakk.Web.Helpers;
using Snakk.Web.Pages.ViewModels;
using Snakk.Web.Services;

namespace Snakk.Web.Pages.Partials;

public class SavedPostsModel(
    SnakkApiClient apiClient,
    ICommunityContext communityContext) : PageModel
{
    public ICommunityContext Community => communityContext;
    public List<PostListItemVM> Items { get; set; } = [];
    public bool HasMoreItems { get; set; }
    public int NextOffset { get; set; }

    public async Task<IActionResult> OnGetAsync(int offset = 0, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!HttpContext.Request.Cookies.ContainsKey(AuthCookieHelper.AccessCookieName))
            return Content("", "text/html");

        pageSize = Math.Clamp(pageSize, 1, 50);

        try
        {
            var result = await apiClient.GetSavedPostsAsync(offset, pageSize);
            if (result != null)
            {
                Items = result.Items.Select(PostListItemVM.FromSavedPost).ToList();
                HasMoreItems = result.HasMoreItems;
                NextOffset = offset + Items.Count;
            }
        }
        catch { }

        return Page();
    }
}
