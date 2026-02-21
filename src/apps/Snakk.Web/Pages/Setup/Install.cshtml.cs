using Microsoft.AspNetCore.Mvc;
using Snakk.Web.Services;

namespace Snakk.Web.Pages.Setup;

public class InstallModel : SetupPageBase
{
    private readonly SetupService _setupService;

    public InstallModel(SetupService setupService) => _setupService = setupService;

    public void OnGet() => ViewData["SetupStep"] = 10;

    public async Task<IActionResult> OnPostAsync()
    {
        var state = GetState();

        try
        {
            // Step 1: Write appsettings.Production.json
            _setupService.WriteProductionConfig(state);

            // Step 2: Run DbSeeder (migrations + admin user + optional seed data)
            var (success, output) = await _setupService.RunDbSeederAsync(state);
            if (!success)
                return new JsonResult(new { success = false, step = "database", error = output });

            // Step 3: Create .setup-complete marker file
            _setupService.MarkSetupComplete(state.AvatarStoragePath);

            // Step 4: Generate JWT for auto-login after restart
            var jwt = await _setupService.GenerateAdminJwtAsync(state);
            Response.Cookies.Append(".Snakk.Auth", jwt, new CookieOptions
            {
                HttpOnly = true,
                Secure = !HttpContext.Request.Host.Host.Contains("localhost"),
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddHours(1)
            });

            // Clear session (contains sensitive data like admin password)
            HttpContext.Session.Clear();

            return new JsonResult(new { success = true });
        }
        catch (Exception ex)
        {
            return new JsonResult(new { success = false, step = "unknown", error = ex.Message });
        }
    }
}
