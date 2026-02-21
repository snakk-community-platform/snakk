using Microsoft.AspNetCore.Mvc;

namespace Snakk.Web.Pages.Setup;

public class AdminAccountModel : SetupPageBase
{
    [BindProperty] public string AdminEmail { get; set; } = "";
    [BindProperty] public string AdminDisplayName { get; set; } = "";
    [BindProperty] public string AdminPassword { get; set; } = "";
    [BindProperty] public string ConfirmPassword { get; set; } = "";

    public void OnGet()
    {
        ViewData["SetupStep"] = 5;
        var state = GetState();
        AdminEmail = state.AdminEmail;
        AdminDisplayName = state.AdminDisplayName;
    }

    public IActionResult OnPost()
    {
        ViewData["SetupStep"] = 5;

        if (string.IsNullOrWhiteSpace(AdminEmail))
            ModelState.AddModelError("AdminEmail", "Email is required.");

        if (string.IsNullOrWhiteSpace(AdminDisplayName))
            ModelState.AddModelError("AdminDisplayName", "Display name is required.");

        if (string.IsNullOrWhiteSpace(AdminPassword) || AdminPassword.Length < 8)
            ModelState.AddModelError("AdminPassword", "Password must be at least 8 characters.");

        if (AdminPassword != ConfirmPassword)
            ModelState.AddModelError("ConfirmPassword", "Passwords do not match.");

        if (!ModelState.IsValid)
            return Page();

        var state = GetState();
        state.AdminEmail = AdminEmail.Trim();
        state.AdminDisplayName = AdminDisplayName.Trim();
        state.AdminPassword = AdminPassword;
        SaveState(state);
        return RedirectToPage("TestData");
    }
}
