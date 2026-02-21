using Microsoft.AspNetCore.Mvc;

namespace Snakk.Web.Pages.Setup;

public class StorageModel : SetupPageBase
{
    [BindProperty] public string AvatarStoragePath { get; set; } = "/app/storage";

    public void OnGet()
    {
        ViewData["SetupStep"] = 4;
        var state = GetState();
        AvatarStoragePath = state.AvatarStoragePath;
    }

    public IActionResult OnPost()
    {
        ViewData["SetupStep"] = 4;
        if (string.IsNullOrWhiteSpace(AvatarStoragePath))
        {
            ModelState.AddModelError("AvatarStoragePath", "Storage path is required.");
            return Page();
        }

        var state = GetState();
        state.AvatarStoragePath = AvatarStoragePath.Trim();
        SaveState(state);
        return RedirectToPage("AdminAccount");
    }
}
