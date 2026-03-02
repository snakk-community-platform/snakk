using Microsoft.AspNetCore.Mvc;

namespace Snakk.Web.Pages.Setup;

public class SiteConfigModel : SetupPageBase
{
    [BindProperty] public string Domain { get; set; } = "";
    [BindProperty] public string SiteName { get; set; } = "Snakk";
    [BindProperty] public string DefaultCommunitySlug { get; set; } = "main";
    [BindProperty] public bool MultiCommunityEnabled { get; set; }

    public void OnGet()
    {
        ViewData["SetupStep"] = 3;
        var state = GetState();
        Domain = !string.IsNullOrEmpty(state.Domain) ? state.Domain : HttpContext.Request.Host.Host;
        SiteName = state.SiteName;
        DefaultCommunitySlug = state.DefaultCommunitySlug;
        MultiCommunityEnabled = state.MultiCommunityEnabled;
    }

    public IActionResult OnPost()
    {
        ViewData["SetupStep"] = 3;
        if (string.IsNullOrWhiteSpace(Domain))
        {
            ModelState.AddModelError("Domain", "Domain is required.");
            return Page();
        }

        var state = GetState();
        state.Domain = Domain.Trim();
        state.SiteName = SiteName.Trim();
        state.DefaultCommunitySlug = DefaultCommunitySlug.Trim().ToLowerInvariant();
        state.MultiCommunityEnabled = MultiCommunityEnabled;
        SaveState(state);

        return RedirectToPage("Storage");
    }
}
