using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;
using Snakk.Shared;

namespace Snakk.Web.Pages.My.Settings;

public class SocialModel : PageModel
{
    public string PlatformsJson { get; private set; } = "[]";

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated != true)
            return Redirect($"/auth/login?returnUrl={Uri.EscapeDataString(Request.Path + Request.QueryString)}");

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
