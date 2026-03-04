using Microsoft.AspNetCore.Mvc;

namespace Snakk.Setup.Pages;

public class IndexModel : SetupPageBase
{
    public void OnGet() => ViewData["SetupStep"] = 1;
    public IActionResult OnPost() => RedirectToPage("Database");
}
