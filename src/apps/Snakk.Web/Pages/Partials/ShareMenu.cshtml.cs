using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Snakk.Web.Pages.Partials;

public class ShareMenuModel : PageModel
{
    public string ShareUrl { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public bool IsPublic { get; set; } = true;

    public void OnGet(string url, string title, bool isPublic = true)
    {
        Response.Headers.CacheControl = "no-store";
        ShareUrl = url;
        Title = title;
        IsPublic = isPublic;
    }
}
