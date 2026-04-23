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

        if (!string.IsNullOrEmpty(ReturnUrl))
        {
            HttpContext.Session.SetString("OAuth_ReturnUrl", ReturnUrl);
        }

        // CSRF is handled by the OAuth handler's correlation cookie.
        var properties = new AuthenticationProperties
        {
            RedirectUri = Url.Page("/OAuth/Callback", new { provider = Provider })
        };

        return Challenge(properties, Provider);
    }
}
