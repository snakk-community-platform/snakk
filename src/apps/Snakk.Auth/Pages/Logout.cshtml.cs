using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Snakk.Auth.Pages;

public class LogoutModel : PageModel
{
    public IActionResult OnGet()
    {
        // Delete JWT cookie
        Response.Cookies.Delete(".Snakk.Auth", new CookieOptions
        {
            Path = "/",
            Domain = null
        });

        // Redirect to home
        return Redirect("/");
    }

    public IActionResult OnPost()
    {
        return OnGet();
    }
}
