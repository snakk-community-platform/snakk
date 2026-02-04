using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Snakk.Web.Services;

namespace Snakk.Web.Pages.Auth;

public class ProfileModel(SnakkApiClient apiClient, IConfiguration configuration) : PageModel
{
    private readonly SnakkApiClient _apiClient = apiClient;

    public string? UserId { get; set; }
    public string? DisplayName { get; set; }
    public string? Email { get; set; }
    public bool EmailVerified { get; set; }
    public string? OAuthProvider { get; set; }
    public bool PreferEndlessScroll { get; set; } = true;
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }

    public string ApiBaseUrl => configuration["ApiBaseUrl"] ?? "https://localhost:7291";

    public IActionResult OnGetAsync()
    {
        // Authentication check handled client-side via profile.js
        // since JWT tokens are stored in localStorage (not accessible server-side)
        return Page();
    }

    // Form posts are removed - all profile updates now happen via client-side fetch
}
