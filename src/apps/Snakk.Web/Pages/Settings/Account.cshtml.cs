using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Snakk.Web.Services;

namespace Snakk.Web.Pages.Settings;

public class AccountModel(IConfiguration configuration) : PageModel
{
    public string? UserId { get; set; }
    public string? TurnstileSiteKey => configuration["Turnstile:SiteKey"];

    public IActionResult OnGet()
    {
        UserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (UserId is null) return Redirect("/auth/login?returnUrl=/settings/account");
        return Page();
    }
}
