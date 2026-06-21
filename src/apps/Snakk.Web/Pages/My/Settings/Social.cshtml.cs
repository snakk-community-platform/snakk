using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Snakk.Shared;
using Snakk.Web.Services;

namespace Snakk.Web.Pages.My.Settings;

public class SocialModel(IConfiguration configuration, ICommunityContext communityContext) : BasePageModel(configuration, communityContext)
{
    public string PlatformsJson { get; private set; } = "[]";

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated != true)
            return Redirect($"/auth/login?returnUrl={Uri.EscapeDataString(Request.Path + Request.QueryString)}");

        Preload("settings");
        var platforms = SocialPlatformRegistry.All.Values
            .Select(p => new
            {
                key = p.Key,
                displayName = p.DisplayName,
                category = p.Category,
                placeholder = p.Placeholder,
                usernamePattern = p.UsernamePattern,
                hasUrl = p.UrlTemplate is not null || p.Key == "mastodon"
            })
            .ToList();

        PlatformsJson = JsonSerializer.Serialize(platforms);
        return Page();
    }
}
