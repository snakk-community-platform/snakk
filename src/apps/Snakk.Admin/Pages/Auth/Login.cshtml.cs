using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Snakk.Admin.Pages.Auth;

[AllowAnonymous]
public class LoginModel : PageModel
{
    public IActionResult OnGet(string? returnUrl = null)
    {
        // Redirect to SSO login service
        var target = returnUrl ?? "/admin";
        var loginUrl = $"/auth/login?returnUrl={Uri.EscapeDataString(target)}";

        return Redirect(loginUrl);
    }
}
