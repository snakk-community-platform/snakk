using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Snakk.Web.Pages.My.Settings;

public class NotificationsModel : PageModel
{
    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated != true)
            return Redirect($"/auth/login?returnUrl={Uri.EscapeDataString(Request.Path + Request.QueryString)}");
        return Page();
    }
}
