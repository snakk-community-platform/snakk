using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Snakk.AdminWeb.Pages.Auth;

public class LogoutModel : PageModel
{
    public IActionResult OnGet()
    {
        return PerformLogout();
    }

    public IActionResult OnPost()
    {
        return PerformLogout();
    }

    private IActionResult PerformLogout()
    {
        // Delete SSO auth cookie
        Response.Cookies.Delete(".Snakk.Auth", new CookieOptions
        {
            Path = "/",
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax
        });

        return Redirect("/auth/login");
    }
}
