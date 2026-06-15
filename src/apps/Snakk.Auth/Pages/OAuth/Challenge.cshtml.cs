using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Snakk.Auth.Services;

namespace Snakk.Auth.Pages.OAuth;

public class ChallengeModel(IJwtCookieValidator jwtCookieValidator) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string Provider { get; set; } = "";

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool ConnectMode { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool SudoMode { get; set; }

    public IActionResult OnGet()
    {
        if (string.IsNullOrEmpty(Provider))
            return RedirectToPage("/Login");

        if (ConnectMode || SudoMode)
        {
            // Users authenticated via Snakk.Web carry JWT cookies, not the transient
            // Snakk.Auth Cookie scheme. The JWT signature MUST be validated before
            // we trust the sub claim: a previous implementation called
            // JwtSecurityTokenHandler.ReadJwtToken (parse-only) and stored the result
            // in session, which let an attacker forge a JWT containing the victim's
            // sub claim, drop it into the .Snakk.Auth cookie, and drive the OAuth
            // connect/sudo flow against the victim's account
            // (CR-21 in docs/SECURITY-AUDIT-2026-05-23.md).
            var jwtCookie = Request.Cookies[".Snakk.Auth"] ?? Request.Cookies[".Snakk.Auth.Session"];
            var userId = jwtCookieValidator.ValidateAndExtractUserId(jwtCookie);
            if (string.IsNullOrEmpty(userId))
                return RedirectToPage("/Login", new { returnUrl = Request.PathBase + Request.Path + Request.QueryString });

            if (ConnectMode)
            {
                HttpContext.Session.SetString("OAuth_ConnectMode", "true");
                HttpContext.Session.SetString("OAuth_ConnectUserId", userId);
            }
            else
            {
                HttpContext.Session.SetString("OAuth_SudoMode", "true");
                HttpContext.Session.SetString("OAuth_SudoUserId", userId);
            }
        }

        if (!string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
            HttpContext.Session.SetString("OAuth_ReturnUrl", ReturnUrl);

        // CSRF is handled by the OAuth handler's correlation cookie.
        var properties = new AuthenticationProperties
        {
            RedirectUri = Url.Page("/OAuth/Callback", new { provider = Provider })
        };

        return Challenge(properties, Provider);
    }
}
