using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Snakk.Web.Services;

namespace Snakk.Web.Pages.Settings;

public class PrivacyModel(SnakkApiClient apiClient, IConfiguration configuration) : PageModel
{
    public bool HidePresence { get; private set; }
    public bool PasskeysEnabled => configuration.GetValue<bool>("Features:PasskeysEnabled", true);
    public bool TwoFactorEnabled => configuration.GetValue<bool>("Features:TwoFactorEnabled", true);

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (User.Identity?.IsAuthenticated == true)
        {
            var me = await apiClient.GetCurrentUserAsync();
            HidePresence = me?.HidePresence ?? false;
        }
        return Page();
    }
}
