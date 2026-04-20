using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Snakk.Web.Helpers;
using Snakk.Web.Pages.ViewModels;
using Snakk.Web.Services;
using System.Security.Claims;

namespace Snakk.Web.Pages.Partials;

public class MyPostsModel(
    SnakkApiClient apiClient,
    ICommunityContext communityContext) : PageModel
{
    public ICommunityContext Community => communityContext;
    public List<PostListItemVM> Items { get; set; } = [];
    public bool HasMoreItems { get; set; }
    public int NextOffset { get; set; }

    public async Task<IActionResult> OnGetAsync(int offset = 0, int pageSize = 20)
    {
        if (!HttpContext.Request.Cookies.ContainsKey(AuthCookieHelper.AccessCookieName))
            return Content("", "text/html");

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
            return Content("", "text/html");

        pageSize = Math.Clamp(pageSize, 1, 50);

        try
        {
            var result = await apiClient.SearchPostsAsync(
                authorPublicId: userId, offset: offset, pageSize: pageSize);
            if (result != null)
            {
                Items = result.Items.Select(PostListItemVM.FromSearchResult).ToList();
                HasMoreItems = result.HasMoreItems;
                NextOffset = offset + Items.Count;
            }
        }
        catch { }

        return Page();
    }
}
