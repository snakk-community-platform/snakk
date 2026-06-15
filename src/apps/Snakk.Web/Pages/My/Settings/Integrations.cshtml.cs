using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Snakk.Web.Pages.My.Settings;

public class IntegrationsModel : PageModel
{
    public string? DiscordStatus { get; set; }
    public string? DiscordErrorReason { get; set; }

    public IActionResult OnGet(
        [FromQuery(Name = "discord")] string? discord,
        [FromQuery(Name = "reason")] string? reason)
    {
        if (User.Identity?.IsAuthenticated != true)
            return Redirect($"/auth/login?returnUrl={Uri.EscapeDataString(Request.Path + Request.QueryString)}");
        DiscordStatus = discord;
        DiscordErrorReason = reason;
        return Page();
    }
}
