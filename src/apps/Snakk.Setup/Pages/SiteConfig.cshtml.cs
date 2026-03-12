using Microsoft.AspNetCore.Mvc;

namespace Snakk.Setup.Pages;

public class SiteConfigModel : SetupPageBase
{
    [BindProperty] public string Domain { get; set; } = "";
    [BindProperty] public string SiteName { get; set; } = "Snakk";
    [BindProperty] public string DefaultCommunitySlug { get; set; } = "main";
    [BindProperty] public bool MultiCommunityEnabled { get; set; }
    [BindProperty] public string Timezone { get; set; } = "UTC";

    public void OnGet()
    {
        ViewData["SetupStep"] = 3;
        var state = GetState();
        Domain = !string.IsNullOrEmpty(state.Domain) ? state.Domain : HttpContext.Request.Host.Host;
        SiteName = state.SiteName;
        DefaultCommunitySlug = state.DefaultCommunitySlug;
        MultiCommunityEnabled = state.MultiCommunityEnabled;
        Timezone = state.Timezone;
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
        state.Timezone = string.IsNullOrWhiteSpace(Timezone) ? "UTC" : Timezone;
        SaveState(state);

        return RedirectToPage("Storage");
    }
}
