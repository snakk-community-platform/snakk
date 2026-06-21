using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Snakk.Shared;
using Snakk.Web.Services;

namespace Snakk.Web.Pages.My.Settings;

public class AccountModel(IConfiguration configuration, ICommunityContext communityContext) : BasePageModel(configuration, communityContext)
{
    public string? UserId { get; set; }
    public string? TurnstileSiteKey => configuration["Turnstile:SiteKey"];
    public string PlatformsJson { get; private set; } = "[]";

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated != true)
            return Redirect($"/auth/login?returnUrl={Uri.EscapeDataString(Request.Path + Request.QueryString)}");

        Preload("settings");
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
