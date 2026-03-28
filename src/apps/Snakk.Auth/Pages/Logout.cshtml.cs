using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Snakk.Auth.Pages;

public class LogoutModel : PageModel
{
    public IActionResult OnGet()
    {
        // Delete all auth cookies (Strict + Lax session + Refresh)
        var deleteOptions = new CookieOptions { Path = "/" };
        Response.Cookies.Delete(".Snakk.Auth", deleteOptions);
        Response.Cookies.Delete(".Snakk.Auth.Session", deleteOptions);
        Response.Cookies.Delete(".Snakk.Auth.Refresh", deleteOptions);

        // Redirect to home
        return Redirect("/");
    }

    public IActionResult OnPost() =>
        OnGet();
}
