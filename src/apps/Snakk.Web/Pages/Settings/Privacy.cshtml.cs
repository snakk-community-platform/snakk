using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Snakk.Web.Pages.Settings;

public class PrivacyModel : PageModel
{
    public IActionResult OnGet()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (userId is null) return Redirect("/auth/login?returnUrl=/settings/privacy");
        return Page();
    }
}
