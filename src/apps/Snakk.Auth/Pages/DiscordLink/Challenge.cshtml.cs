using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Snakk.Auth.Pages.DiscordLink;

public class ChallengeModel : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? LinkToken { get; set; }

    public IActionResult OnGet()
    {
        if (string.IsNullOrEmpty(LinkToken))
            return RedirectToPage("/Login");

        HttpContext.Session.SetString("DiscordLink_Token", LinkToken);

        var properties = new AuthenticationProperties
        {
            RedirectUri = Url.Page("/DiscordLink/Callback")
        };

        return Challenge(properties, "DiscordLink");
    }
}
