using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Snakk.Web.Pages.Auth;

public class SetupProfileModel : PageModel
{
    public string? SuggestedDisplayName { get; set; }
    public string? ErrorMessage { get; set; }

    public void OnGet()
    {
        // Auth cookies are already set by Snakk.Auth before redirecting here.
        // The form submission is handled by JavaScript calling /bff/auth/update-profile.
    }
}
