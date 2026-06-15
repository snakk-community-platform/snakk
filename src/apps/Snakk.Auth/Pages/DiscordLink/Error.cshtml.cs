using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Snakk.Auth.Pages.DiscordLink;

public class ErrorModel : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Reason { get; set; }

    public IActionResult OnGet()
    {
        return Redirect($"/my/settings/integrations?discord=error&reason={Reason ?? "unknown"}");
    }
}
