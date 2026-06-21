using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Snakk.Web.Services;

namespace Snakk.Web.Pages.My.Settings;

public class PrivacyModel(SnakkApiClient apiClient, IConfiguration configuration, ICommunityContext communityContext, IOptions<OAuthProvidersOptions> oauthOptions) : BasePageModel(configuration, communityContext)
{
    public bool HidePresence { get; private set; }
    public bool DisableHistory { get; private set; }
    public bool PasskeysEnabled => configuration.GetValue<bool>("Features:PasskeysEnabled", true);
    public bool TwoFactorEnabled => configuration.GetValue<bool>("Features:TwoFactorEnabled", true);
    public bool OAuthProvidersEnabled => oauthOptions.Value.HasAny;
    public string? ConnectedProvider { get; private set; }
    public string? ErrorCode { get; private set; }

    public async Task<IActionResult> OnGetAsync(
        [FromQuery(Name = "connected")] string? connected,
        [FromQuery(Name = "error")] string? error,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (User.Identity?.IsAuthenticated != true)
            return Redirect($"/auth/login?returnUrl={Uri.EscapeDataString(Request.Path + Request.QueryString)}");
        Preload("settings");
        ConnectedProvider = connected;
        ErrorCode = error;
        var me = await apiClient.GetCurrentUserAsync();
        HidePresence = me?.HidePresence ?? false;
        DisableHistory = Request.Cookies[".Snakk.Pref.DisableHistory"] == "1";
        return Page();
    }
}
