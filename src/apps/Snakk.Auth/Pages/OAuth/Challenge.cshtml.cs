using System.Security.Cryptography;
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

        // Generate cryptographic random state to prevent CSRF on OAuth callback
        var state = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        HttpContext.Session.SetString("OAuth_State", state);

        // Redirect to OAuth provider
        var properties = new AuthenticationProperties
        {
            RedirectUri = Url.Page("/OAuth/Callback", new { provider = Provider }),
            Items = { ["state"] = state }
        };

        return Challenge(properties, Provider);
    }
}
