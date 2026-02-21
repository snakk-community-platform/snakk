using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Snakk.Auth.Pages.OAuth;

public class ChallengeModel : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string Provider { get; set; } = "";

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public IActionResult OnGet()
    {
        if (string.IsNullOrEmpty(Provider))
        {
            return RedirectToPage("/Login");
        }

        // Store return URL in session for callback
        if (!string.IsNullOrEmpty(ReturnUrl))
        {
            HttpContext.Session.SetString("OAuth_ReturnUrl", ReturnUrl);
        }

        // Redirect to OAuth provider
        var properties = new AuthenticationProperties
        {
            RedirectUri = Url.Page("/OAuth/Callback", new { provider = Provider })
        };

        return Challenge(properties, Provider);
    }
}
