using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;
using Snakk.Shared;

namespace Snakk.Web.Pages.My.Settings;

public class AccountModel(IConfiguration configuration) : PageModel
{
    public string? UserId { get; set; }
    public string? TurnstileSiteKey => configuration["Turnstile:SiteKey"];
    public string PlatformsJson { get; private set; } = "[]";

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated != true)
            return Redirect($"/auth/login?returnUrl={Uri.EscapeDataString(Request.Path + Request.QueryString)}");

        UserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        PlatformsJson = JsonSerializer.Serialize(
            SocialPlatformRegistry.All.Values.Select(p => new {
                key = p.Key, displayName = p.DisplayName, category = p.Category,
                placeholder = p.Placeholder, usernamePattern = p.UsernamePattern,
                hasUrl = p.UrlTemplate is not null || p.Key == "mastodon"
            }).ToList());
        return Page();
    }
}
