using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Snakk.Web.Services;

namespace Snakk.Web.Pages.Settings;

public class PrivacyModel(SnakkApiClient apiClient) : PageModel
{
    public bool HidePresence { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            var me = await apiClient.GetCurrentUserAsync();
            HidePresence = me?.HidePresence ?? false;
        }
        return Page();
    }
}
