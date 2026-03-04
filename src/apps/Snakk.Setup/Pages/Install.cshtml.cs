using Microsoft.AspNetCore.Mvc;
using Snakk.Setup.Services;

namespace Snakk.Setup.Pages;

public class InstallModel(SetupService setupService) : SetupPageBase
{
    public void OnGet() => ViewData["SetupStep"] = 10;

    public IActionResult OnPost()
    {
        ViewData["SetupStep"] = 10;
        var state = GetState();
        setupService.StartInstallInBackground(state);

        return new JsonResult(new { started = true });
    }

    public IActionResult OnGetStatus() =>
        new JsonResult(new
        {
            step = InstallProgress.Step,
            message = InstallProgress.Message,
            isComplete = InstallProgress.Step == "complete",
            hasError = InstallProgress.HasError,
            errorMessage = InstallProgress.ErrorMessage,
            seedEnabled = InstallProgress.SeedEnabled
        });

    public IActionResult OnPostFinalize()
    {
        // Validate that installation actually completed
        if (InstallProgress.Step != "complete" || InstallProgress.Jwt is null)
            return new JsonResult(new { success = false, error = "Installation not complete." });

        // Set JWT cookie for auto-login as admin
        Response.Cookies.Append(".Snakk.Auth", InstallProgress.Jwt, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            MaxAge = TimeSpan.FromHours(1)
        });

        // Write .setup-complete marker file (triggers Docker entrypoint to restart services)
        var state = GetState();
        setupService.MarkSetupComplete(state.AvatarStoragePath);

        // Clear session and progress
        HttpContext.Session.Clear();
        InstallProgress.Reset();

        return new JsonResult(new { success = true });
    }
}
