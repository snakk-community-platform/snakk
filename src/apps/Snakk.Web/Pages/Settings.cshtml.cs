using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Snakk.Web.Services;

namespace Snakk.Web.Pages;

public class SettingsModel(SnakkApiClient apiClient, IConfiguration configuration) : PageModel
{
    private readonly SnakkApiClient _apiClient = apiClient;

    public string? UserId { get; set; }
    public string? DisplayName { get; set; }
    public string? Email { get; set; }
    public bool EmailVerified { get; set; }
    public string? OAuthProvider { get; set; }
    public bool AutoFollowOnReply { get; set; } = true;
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }

    public string ApiBaseUrl => configuration["ApiBaseUrl"] ?? "https://localhost:17100";
    public string? TurnstileSiteKey => configuration["Turnstile:SiteKey"];

    public IActionResult OnGetAsync()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Page();
        }

        return Redirect("/settings/account");
    }

    // Form posts are removed - all profile updates now happen via client-side fetch
}
