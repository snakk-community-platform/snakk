using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Snakk.Web.Pages.My.Settings;

public class BrowserModel : PageModel
{
    public IActionResult OnGet()
    {
        return Page();
    }
}
