using Microsoft.AspNetCore.Mvc;

namespace Snakk.Setup.Pages;

public class IndexModel : SetupPageBase
{
    [BindProperty]
    public string? Password { get; set; }

    public bool RequiresPassword => !string.IsNullOrEmpty(
        Environment.GetEnvironmentVariable("SETUP_PASSWORD"));

    public bool ShowError { get; set; }

    public void OnGet() => ViewData["SetupStep"] = 1;

    public IActionResult OnPost()
    {
        var expected = Environment.GetEnvironmentVariable("SETUP_PASSWORD");

        if (!string.IsNullOrEmpty(expected))
        {
            if (!string.Equals(Password, expected, StringComparison.Ordinal))
            {
                ShowError = true;
                ViewData["SetupStep"] = 1;
                return Page();
            }

            HttpContext.Session.SetString("SetupAuthenticated", "true");
        }

        return RedirectToPage("Database");
    }
}
