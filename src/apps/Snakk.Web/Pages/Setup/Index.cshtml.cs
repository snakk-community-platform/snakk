using Microsoft.AspNetCore.Mvc;

namespace Snakk.Web.Pages.Setup;

public class IndexModel : SetupPageBase
{
    public void OnGet() => ViewData["SetupStep"] = 1;
    public IActionResult OnPost() => RedirectToPage("Database");
}
