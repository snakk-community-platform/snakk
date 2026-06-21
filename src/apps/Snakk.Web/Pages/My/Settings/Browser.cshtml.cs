using Microsoft.AspNetCore.Mvc;
using Snakk.Web.Services;

namespace Snakk.Web.Pages.My.Settings;

public class BrowserModel(IConfiguration configuration, ICommunityContext communityContext) : BasePageModel(configuration, communityContext)
{
    public IActionResult OnGet()
    {
        Preload("settings");
        return Page();
    }
}
