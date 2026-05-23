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
            // Snakk.Auth Cookie scheme — read the JWT directly to identify the user.
            var jwtCookie = Request.Cookies[".Snakk.Auth"] ?? Request.Cookies[".Snakk.Auth.Session"];
            var userId = GetUserIdFromJwt(jwtCookie);
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

        if (!string.IsNullOrEmpty(ReturnUrl))
            HttpContext.Session.SetString("OAuth_ReturnUrl", ReturnUrl);

        // CSRF is handled by the OAuth handler's correlation cookie.
        var properties = new AuthenticationProperties
        {
            RedirectUri = Url.Page("/OAuth/Callback", new { provider = Provider })
        };

        return Challenge(properties, Provider);
    }

    private static string? GetUserIdFromJwt(string? token)
    {
        if (string.IsNullOrEmpty(token)) return null;
        try
        {
            var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
            if (!handler.CanReadToken(token)) return null;
            var jwt = handler.ReadJwtToken(token);
            if (jwt.ValidTo <= DateTime.UtcNow) return null;
            return jwt.Subject
                ?? jwt.Claims.FirstOrDefault(c => c.Type == "sub")?.Value;
        }
        catch { return null; }
    }
}
