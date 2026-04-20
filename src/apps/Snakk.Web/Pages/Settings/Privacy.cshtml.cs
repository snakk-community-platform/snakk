using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Snakk.Web.Pages.Settings;

public class PrivacyModel : PageModel
{
    public IActionResult OnGet()
    {
        return Page();
    }
}
