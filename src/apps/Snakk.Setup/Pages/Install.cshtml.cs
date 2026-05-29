using Microsoft.AspNetCore.Mvc;
using Snakk.Setup.Services;

namespace Snakk.Setup.Pages;

public class InstallModel(SetupService setupService) : SetupPageBase
{
    public void OnGet() => ViewData["SetupStep"] = 13;

    public IActionResult OnPost()
    {
        ViewData["SetupStep"] = 13;
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
            Secure = Snakk.Shared.Helpers.AuthCookieSecurity.RequireSecure,
            SameSite = SameSiteMode.Strict,
            MaxAge = TimeSpan.FromHours(1)
        });

        // Scrub sensitive data from config (snakk-config.json is already written)
        var state = GetState();
        setupService.ScrubSensitiveConfig(state.AvatarStoragePath);

        // Clear session and progress
        HttpContext.Session.Clear();
        InstallProgress.Reset();

        return new JsonResult(new { success = true });
    }
}
