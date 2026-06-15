using Microsoft.AspNetCore.Mvc;
using Snakk.Web.Helpers;
using Snakk.Web.Services;

namespace Snakk.Web.Pages.My;

public class HistoryModel(IConfiguration configuration, ICommunityContext communityContext)
    : BasePageModel(configuration, communityContext)
{
    public bool IsAuthenticated { get; set; }

    public IActionResult OnGet()
    {
        IsAuthenticated = HttpContext.Request.Cookies.ContainsKey(AuthCookieHelper.AccessCookieName);
        return Page();
    }
}
