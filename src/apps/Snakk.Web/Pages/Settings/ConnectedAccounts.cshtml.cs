using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Snakk.Web.Pages.Settings;

public class ConnectedAccountsModel : PageModel
{
    public string? ConnectedProvider { get; set; }
    public string? ErrorCode { get; set; }

    public IActionResult OnGet(
        [FromQuery(Name = "connected")] string? connected,
        [FromQuery(Name = "error")] string? error)
    {
        var qs = new List<string>();
        if (!string.IsNullOrEmpty(connected)) qs.Add($"connected={Uri.EscapeDataString(connected)}");
        if (!string.IsNullOrEmpty(error)) qs.Add($"error={Uri.EscapeDataString(error)}");
        var target = "/settings/privacy" + (qs.Count > 0 ? "?" + string.Join("&", qs) : "");
        return Redirect(target);
    }
}
