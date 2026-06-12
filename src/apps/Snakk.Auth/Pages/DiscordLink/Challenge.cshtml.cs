using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Snakk.Auth.Services;

namespace Snakk.Auth.Pages.DiscordLink;

public class ChallengeModel(IJwtCookieValidator jwtValidator) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? LinkToken { get; set; }

    public IActionResult OnGet()
    {
        if (string.IsNullOrEmpty(LinkToken))
            return RedirectToPage("/Login");

        // Discord linking attaches a Discord identity to the *currently authenticated*
        // user. Require a valid auth cookie so an unauthenticated visitor who obtains a
        // link token (e.g. from logs/history) cannot drive the link flow.
        var userId = jwtValidator.ValidateAndExtractUserId(
            Request.Cookies[".Snakk.Auth"] ?? Request.Cookies[".Snakk.Auth.Session"]);
        if (string.IsNullOrEmpty(userId))
            return RedirectToPage("/Login");

        HttpContext.Session.SetString("DiscordLink_Token", LinkToken);

        var properties = new AuthenticationProperties
        {
            RedirectUri = Url.Page("/DiscordLink/Callback")
        };

        return Challenge(properties, "DiscordLink");
    }
}
