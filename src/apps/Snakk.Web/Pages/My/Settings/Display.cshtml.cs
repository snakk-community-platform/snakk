using Microsoft.AspNetCore.Mvc;
using Snakk.Web.Services;

namespace Snakk.Web.Pages.My.Settings;

public class DisplayModel(IConfiguration configuration, ICommunityContext communityContext) : BasePageModel(configuration, communityContext)
{
    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated != true)
            return Redirect($"/auth/login?returnUrl={Uri.EscapeDataString(Request.Path + Request.QueryString)}");
        Preload("settings");
        return Page();
    }
}
