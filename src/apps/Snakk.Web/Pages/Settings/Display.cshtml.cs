using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Snakk.Web.Pages.Settings;

public class DisplayModel : PageModel
{
    public IActionResult OnGet()
    {
        return Page();
    }
}
