using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Snakk.Web.Pages.Settings;

public class NotificationsModel : PageModel
{
    public IActionResult OnGet()
    {
        return Page();
    }
}
