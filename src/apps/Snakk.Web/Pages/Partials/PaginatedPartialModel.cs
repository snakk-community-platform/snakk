using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Snakk.Web.Services;

namespace Snakk.Web.Pages.Partials;

public abstract class PaginatedPartialModel : PageModel
{
    public int Offset { get; protected set; }
    public int NextOffset { get; protected set; }
    public int MaxOffset { get; protected set; }
    public bool HasMoreItems { get; protected set; }

    protected IActionResult? RequireAuthCookie()
        => HttpContext.Request.Cookies.ContainsKey(AuthCookieHelper.AccessCookieName)
            ? null
            : Content("", "text/html");

    protected (int pageSize, bool exceeded) ApplyPaginationGuard(
        int offset, int rawPageSize, int minPageSize, int maxPageSize, IConfiguration configuration)
    {
        var pageSize = Math.Clamp(rawPageSize, minPageSize, maxPageSize);
        Offset = offset;
        var maxPages = configuration.GetValue("EndlessScroll:MaxPages", 10);
        MaxOffset = maxPages * pageSize;
        var exceeded = offset >= MaxOffset;
        if (exceeded) HasMoreItems = false;
        return (pageSize, exceeded);
    }
}
