using Microsoft.AspNetCore.Mvc;
using Snakk.Setup.Services;

namespace Snakk.Setup.Pages;

public class ReviewModel : SetupPageBase
{
    public SetupState State { get; set; } = new();

    public void OnGet()
    {
        ViewData["SetupStep"] = 9;
        State = GetState();
    }

    public IActionResult OnPost() =>
        RedirectToPage("Install");
}
