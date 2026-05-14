using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Snakk.Admin.Pages.Auth;

public class LogoutModel : PageModel
{
    public IActionResult OnGet() =>
        PerformLogout();

    public IActionResult OnPost() =>
        PerformLogout();

    private IActionResult PerformLogout()
    {
        var deleteOptions = new CookieOptions { Path = "/" };
        Response.Cookies.Delete(".Snakk.Auth", deleteOptions);
        Response.Cookies.Delete(".Snakk.Auth.Session", deleteOptions);
        Response.Cookies.Delete(".Snakk.Auth.Refresh", deleteOptions);

        return Redirect("/auth/login");
    }
}
