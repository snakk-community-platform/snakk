using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Snakk.Web.Pages.Auth;

public class OAuthChallengeModel : PageModel
{
    private readonly IConfiguration _configuration;

    public OAuthChallengeModel(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public IActionResult OnGet(string provider, string? returnUrl)
    {
        var apiBaseUrl = _configuration["ApiBaseUrl"] ?? "https://localhost:7291";
        var webClientUrl = $"{Request.Scheme}://{Request.Host}";

        // Default returnUrl to Web client root if not specified
        var finalReturnUrl = !string.IsNullOrEmpty(returnUrl) ? returnUrl : webClientUrl;

        // Redirect to API's OAuth challenge endpoint
        var apiOAuthUrl = $"{apiBaseUrl}/auth/oauth/{provider}/challenge?returnUrl={Uri.EscapeDataString(finalReturnUrl)}";

        return Redirect(apiOAuthUrl);
    }
}
