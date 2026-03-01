using Microsoft.AspNetCore.Mvc;
using Snakk.Web.Services;

namespace Snakk.Web.Pages.Setup;

[IgnoreAntiforgeryToken]
public class InstallModel : SetupPageBase
{
    private readonly SetupService _setupService;

    public InstallModel(SetupService setupService) => _setupService = setupService;

    public void OnGet() => ViewData["SetupStep"] = 10;

    /// <summary>
    /// Start the installation in the background. Returns immediately.
    /// </summary>
    public IActionResult OnPost()
    {
        if (InstallProgress.IsRunning)
            return new JsonResult(new { started = true, alreadyRunning = true });

        if (InstallProgress.Step == "complete")
            return new JsonResult(new { started = true, alreadyComplete = true });

        var state = GetState();
        _setupService.StartInstallInBackground(state);
        return new JsonResult(new { started = true });
    }

    /// <summary>
    /// Poll for installation progress.
    /// </summary>
    public IActionResult OnGetStatus()
    {
        return new JsonResult(new
        {
            step = InstallProgress.Step,
            message = InstallProgress.Message,
            isComplete = InstallProgress.Step == "complete",
            hasError = InstallProgress.HasError,
            errorMessage = InstallProgress.ErrorMessage,
            seedEnabled = InstallProgress.SeedEnabled
        });
    }

    /// <summary>
    /// Called after installation completes. Sets the JWT cookie for auto-login,
    /// then writes the setup-complete marker (which triggers service restart).
    /// </summary>
    public IActionResult OnPostFinalize()
    {
        if (InstallProgress.Step != "complete" || string.IsNullOrEmpty(InstallProgress.Jwt))
            return new JsonResult(new { success = false, error = "Installation not complete." });

        // Read state before clearing session (need storage path for marker file)
        var state = GetState();

        // Set auth cookie for auto-login as admin
        Response.Cookies.Append(".Snakk.Auth", InstallProgress.Jwt, new CookieOptions
        {
            HttpOnly = true,
            Secure = !HttpContext.Request.Host.Host.Contains("localhost"),
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddHours(1)
        });

        // Write .setup-complete marker — this triggers entrypoint.sh to restart services
        _setupService.MarkSetupComplete(state.AvatarStoragePath);

        // Clean up
        HttpContext.Session.Clear();
        InstallProgress.Reset();

        return new JsonResult(new { success = true });
    }
}
