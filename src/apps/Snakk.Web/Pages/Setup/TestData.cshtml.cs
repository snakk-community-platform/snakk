using Microsoft.AspNetCore.Mvc;

namespace Snakk.Web.Pages.Setup;

public class TestDataModel : SetupPageBase
{
    [BindProperty] public bool SeedTestData { get; set; }

    public void OnGet()
    {
        ViewData["SetupStep"] = 6;
        var state = GetState();
        SeedTestData = state.SeedTestData;
    }

    public IActionResult OnPost()
    {
        var state = GetState();
        state.SeedTestData = SeedTestData;
        SaveState(state);
        return RedirectToPage("Security");
    }
}
