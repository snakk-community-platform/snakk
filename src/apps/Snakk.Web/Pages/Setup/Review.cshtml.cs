using Microsoft.AspNetCore.Mvc;
using Snakk.Web.Services;

namespace Snakk.Web.Pages.Setup;

public class ReviewModel : SetupPageBase
{
    public SetupState State { get; set; } = new();

    public void OnGet()
    {
        ViewData["SetupStep"] = 9;
        State = GetState();
    }

    public IActionResult OnPost()
    {
        return RedirectToPage("Install");
    }
}
